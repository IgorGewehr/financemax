using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// <see cref="IConsultorNarrador"/> real — reescreve as <see cref="ConsultorFato.TemplateFallback"/>
/// determinísticas com um LLM barato (gpt-4o-mini por padrão) pra soar mais natural, SEM nunca
/// inventar um número que o motor não calculou (guard-rail: <see cref="ValidacaoAntiAlucinacao"/>).
/// O "piso" continua sendo <see cref="NarradorTemplate"/> — o pior caminho em qualquer ponto desta
/// classe (sem chave, orçamento estourado, chamada HTTP falha, resposta não é JSON válido, frase
/// reprovada na validação) é a frase determinística de sempre, nunca um erro 500 na UI
/// (ADR-0005 §7).
///
/// Ordem de decisão por fato, dentro de um lote:
/// 1. Sem <see cref="OpenAiOptions.ApiKey"/> configurada → 100% template, nem toca em rede/cache/orçamento.
/// 2. Já narrado antes com o MESMO hash de fatos (<see cref="IConsultorNarracaoLlmCache"/>) → reusa, custo 0.
/// 3. Orçamento mensal já estourado (<see cref="IConsultorOrcamentoLlm"/>) → template, sem chamar a OpenAI.
/// 4. Chamada à OpenAI falha/lança/dá timeout → template pro lote inteiro que ia ser narrado.
/// 5. Chamada funciona, mas a frase de UM fato específico reprova a validação anti-alucinação →
///    template só PARA AQUELE fato (os outros do mesmo lote, se válidos, continuam vindo do LLM).
/// </summary>
public sealed class NarradorLlm(
    IOpenAiChatClient cliente,
    NarradorTemplate template,
    IConsultorNarracaoLlmCache cache,
    IConsultorOrcamentoLlm orcamento,
    OpenAiOptions opcoes,
    ILogger<NarradorLlm> logger) : IConsultorNarrador
{
    /// <summary>Preço nominal (USD por 1M tokens) do gpt-4o-mini — só para ESTIMAR o custo que
    /// alimenta o circuito de orçamento (<see cref="IConsultorOrcamentoLlm"/>), não é (nem precisa
    /// ser) uma reconciliação exata de fatura.</summary>
    private const decimal PrecoEntradaUsdPorMilhaoTokens = 0.15m;
    private const decimal PrecoSaidaUsdPorMilhaoTokens = 0.60m;

    /// <summary>Câmbio fixo aproximado — o teto de orçamento é uma trava de segurança grosseira
    /// ("R$20/mês, não R$2000"), não uma cotação em tempo real.</summary>
    private const decimal TaxaCambioUsdParaBrl = 5.5m;

    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ConsultorInsightNarrado>> NarrarAsync(
        IReadOnlyList<ConsultorFato> fatos, CancellationToken ct = default)
    {
        if (fatos.Count == 0) return [];

        // Degradação graciosa — sem chave configurada, nem entra no fluxo de cache/orçamento/rede.
        if (string.IsNullOrWhiteSpace(opcoes.ApiKey))
        {
            return await template.NarrarAsync(fatos, ct).ConfigureAwait(false);
        }

        var resultadoPorChave = new Dictionary<(string Modulo, string RuleId), ConsultorInsightNarrado>();
        var hashPorChave = new Dictionary<(string Modulo, string RuleId), string>();
        var paraChamarLlm = new List<ConsultorFato>();

        foreach (var fato in fatos)
        {
            var chave = (fato.Modulo, fato.RuleId);
            var hash = ConsultorFatoHasher.Hash(fato.Facts);
            hashPorChave[chave] = hash;

            var emCache = cache.ObterSeAtual(fato.Modulo, fato.RuleId, hash);
            if (emCache is not null)
            {
                resultadoPorChave[chave] = emCache;
            }
            else
            {
                paraChamarLlm.Add(fato);
            }
        }

        if (paraChamarLlm.Count > 0)
        {
            IReadOnlyList<ConsultorInsightNarrado> narrados;

            if (!orcamento.PermiteChamada())
            {
                logger.LogWarning(
                    "Super Consultor: orçamento mensal de LLM estourado — {Quantidade} fato(s) caem pro template.",
                    paraChamarLlm.Count);
                narrados = await template.NarrarAsync(paraChamarLlm, ct).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    narrados = await NarrarViaLlmAsync(paraChamarLlm, hashPorChave, ct).ConfigureAwait(false);
                }
                // ct.IsCancellationRequested distingue "o CALLER cancelou" (propaga, não é nosso
                // pra engolir) de "a chamada HTTP deu timeout/erro internamente" (cai pro template
                // — o consultor nunca quebra por causa do LLM, ver classe-mãe).
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Super Consultor: falha ao chamar a OpenAI — caindo para o template.");
                    narrados = await template.NarrarAsync(paraChamarLlm, ct).ConfigureAwait(false);
                }
            }

            foreach (var insight in narrados)
            {
                resultadoPorChave[(insight.Modulo, insight.RuleId)] = insight;
            }
        }

        return fatos.Select(f => resultadoPorChave[(f.Modulo, f.RuleId)]).ToList();
    }

    private async Task<IReadOnlyList<ConsultorInsightNarrado>> NarrarViaLlmAsync(
        IReadOnlyList<ConsultorFato> fatos,
        IReadOnlyDictionary<(string Modulo, string RuleId), string> hashPorChave,
        CancellationToken ct)
    {
        var promptUsuario = MontarPromptUsuario(fatos);
        var chatResultado = await cliente.ChatAsync(PromptSistema, promptUsuario, opcoes.Modelo, ct).ConfigureAwait(false);

        orcamento.RegistrarCusto(EstimarCustoCentavos(chatResultado.PromptTokens, chatResultado.CompletionTokens));

        var frasesPorRuleId = ParsearResposta(chatResultado.Conteudo);

        var resultado = new List<ConsultorInsightNarrado>(fatos.Count);
        var fatosSemFraseValida = new List<ConsultorFato>();

        foreach (var fato in fatos)
        {
            if (frasesPorRuleId.TryGetValue(fato.RuleId, out var frase) && ValidacaoAntiAlucinacao.EhSeguro(fato, frase))
            {
                var insight = new ConsultorInsightNarrado(
                    fato.Modulo, fato.RuleId, fato.Tela, fato.Score, frase, ConsultorNarracaoOrigem.Llm, fato.Facts, fato.Drill);

                cache.Gravar(fato.Modulo, fato.RuleId, hashPorChave[(fato.Modulo, fato.RuleId)], insight);
                resultado.Add(insight);
            }
            else
            {
                if (frasesPorRuleId.ContainsKey(fato.RuleId))
                {
                    logger.LogWarning(
                        "Super Consultor: narração LLM reprovada na validação anti-alucinação para {RuleId} — caindo para o template.",
                        fato.RuleId);
                }

                fatosSemFraseValida.Add(fato);
            }
        }

        if (fatosSemFraseValida.Count > 0)
        {
            resultado.AddRange(await template.NarrarAsync(fatosSemFraseValida, ct).ConfigureAwait(false));
        }

        return resultado;
    }

    private const string PromptSistema =
        """
        Você é o redator do Super Consultor financeiro de um ERP para pequenas empresas brasileiras.
        Você recebe uma lista de fatos JÁ CALCULADOS por um motor determinístico — cada um já tem uma
        frase padrão pronta em "fraseAtual". Sua única tarefa é reescrever "fraseAtual" de forma mais
        natural e fluida, em português do Brasil, mantendo EXATAMENTE os mesmos números e fatos.

        REGRAS ABSOLUTAS:
        1. NUNCA invente, arredonde diferente ou calcule um número novo. Use SOMENTE os números que já
           aparecem em "fatos" ou em "fraseAtual" — nenhum número novo, mesmo que pareça óbvio.
        2. NUNCA adicione conselhos, recomendações ou informações que não estejam nos dados fornecidos.
        3. Cada frase deve ser curta (1-2 frases), tom direto e profissional, como um consultor
           financeiro experiente falando com o dono do negócio.
        4. Responda SOMENTE em JSON válido, no formato exato:
           {"itens":[{"ruleId":"...","frase":"..."}]}
           Um item para cada ruleId recebido, todos na resposta, nenhum a mais.
        """;

    private static string MontarPromptUsuario(IReadOnlyList<ConsultorFato> fatos)
    {
        var itens = fatos.Select(f => new PromptItem(f.RuleId, f.Modulo, f.Facts, f.TemplateFallback));
        return JsonSerializer.Serialize(new { itens }, JsonOpcoes);
    }

    private static Dictionary<string, string> ParsearResposta(string conteudo)
    {
        try
        {
            var resposta = JsonSerializer.Deserialize<RespostaLlm>(conteudo, JsonOpcoes);
            if (resposta?.Itens is null) return [];

            return resposta.Itens
                .Where(i => !string.IsNullOrWhiteSpace(i.RuleId) && !string.IsNullOrWhiteSpace(i.Frase))
                // Em resposta malformada com ruleId duplicado, fica o último — não crasha.
                .GroupBy(i => i.RuleId)
                .ToDictionary(g => g.Key, g => g.Last().Frase);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static long EstimarCustoCentavos(int promptTokens, int completionTokens)
    {
        var custoUsd = promptTokens / 1_000_000m * PrecoEntradaUsdPorMilhaoTokens
            + completionTokens / 1_000_000m * PrecoSaidaUsdPorMilhaoTokens;

        return (long)Math.Ceiling(custoUsd * TaxaCambioUsdParaBrl * 100);
    }

    private sealed record PromptItem(string RuleId, string Modulo, IReadOnlyDictionary<string, string> Fatos, string FraseAtual);

    private sealed record RespostaLlm([property: JsonPropertyName("itens")] IReadOnlyList<RespostaLlmItem>? Itens);

    private sealed record RespostaLlmItem(
        [property: JsonPropertyName("ruleId")] string RuleId,
        [property: JsonPropertyName("frase")] string Frase);
}
