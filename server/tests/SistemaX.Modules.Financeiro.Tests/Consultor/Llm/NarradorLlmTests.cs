using Microsoft.Extensions.Logging.Abstractions;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Abstractions.Consultor.Llm;
using SistemaX.Modules.Financeiro.Tests.Fakes;

namespace SistemaX.Modules.Financeiro.Tests.Consultor.Llm;

/// <summary>
/// Super Consultor com OpenAI — <see cref="NarradorLlm"/> narra os MESMOS números que os fatos já
/// trazem, nunca inventa (guard-rail: <see cref="ValidacaoAntiAlucinacao"/>), cai pro
/// <see cref="NarradorTemplate"/> em qualquer situação que não seja "chamou a OpenAI, recebeu JSON
/// válido, a frase passou na validação". Cliente OpenAI é <see cref="FakeOpenAiChatClient"/> em
/// TODOS os testes — nenhum aqui toca rede de verdade.
/// </summary>
public class NarradorLlmTests
{
    private const string ApiKeyDeTeste = "sk-teste-123";

    private sealed record Ambiente(
        NarradorLlm Narrador,
        FakeOpenAiChatClient ClienteFake,
        InMemoryConsultorOrcamentoLlm Orcamento,
        FakeTimeProvider Relogio);

    private static Ambiente NovoAmbiente(string? apiKey = ApiKeyDeTeste, long orcamentoMensalCentavos = 2_000)
    {
        var opcoes = new OpenAiOptions(apiKey, OpenAiOptions.ModeloPadrao, orcamentoMensalCentavos);
        var relogio = new FakeTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        var orcamento = new InMemoryConsultorOrcamentoLlm(relogio, opcoes);
        var clienteFake = new FakeOpenAiChatClient();
        var cache = new InMemoryConsultorNarracaoLlmCache();
        var template = new NarradorTemplate();

        var narrador = new NarradorLlm(
            clienteFake, template, cache, orcamento, opcoes, NullLogger<NarradorLlm>.Instance);

        return new Ambiente(narrador, clienteFake, orcamento, relogio);
    }

    private static ConsultorFato NovoFato(string ruleId = "fin.runway", string diasTexto = "45") => new(
        Modulo: "financeiro",
        RuleId: ruleId,
        Tela: "visao-geral",
        Score: 5_000,
        Facts: new Dictionary<string, string> { ["runwayDias"] = diasTexto },
        TemplateFallback: $"No ritmo atual, seu caixa aguenta cerca de {diasTexto} dias sem novas vendas (runway realista).",
        Drill: new DrillTarget("visao-geral"));

    private static string RespostaJson(params (string RuleId, string Frase)[] itens)
    {
        var partes = itens.Select(i => $$"""{"ruleId":"{{i.RuleId}}","frase":"{{i.Frase}}"}""");
        return $$"""{"itens":[{{string.Join(",", partes)}}]}""";
    }

    [Fact]
    public async Task NarrarAsync_ComFraseValida_UsaLlm()
    {
        var ambiente = NovoAmbiente();
        var fato = NovoFato();

        ambiente.ClienteFake.EnfileirarResposta(
            RespostaJson((fato.RuleId, "Seu caixa aguenta cerca de 45 dias no ritmo atual — vale ficar de olho.")));

        var resultado = await ambiente.Narrador.NarrarAsync([fato]);

        var insight = Assert.Single(resultado);
        Assert.Equal(ConsultorNarracaoOrigem.Llm, insight.Origem);
        Assert.Equal("Seu caixa aguenta cerca de 45 dias no ritmo atual — vale ficar de olho.", insight.Frase);
        Assert.Equal(1, ambiente.ClienteFake.Chamadas);
    }

    [Fact]
    public async Task NarrarAsync_ComNumeroInventado_DescartaLlmEUsaTemplate()
    {
        var ambiente = NovoAmbiente();
        var fato = NovoFato();

        // 999 não existe em Facts nem em TemplateFallback — número inventado pelo LLM.
        ambiente.ClienteFake.EnfileirarResposta(
            RespostaJson((fato.RuleId, "Seu caixa aguenta cerca de 999 dias no ritmo atual.")));

        var resultado = await ambiente.Narrador.NarrarAsync([fato]);

        var insight = Assert.Single(resultado);
        Assert.Equal(ConsultorNarracaoOrigem.Template, insight.Origem);
        Assert.Equal(fato.TemplateFallback, insight.Frase);
    }

    [Fact]
    public async Task NarrarAsync_QuandoOpenAiLanca_CaiParaTemplateSemQuebrar()
    {
        var ambiente = NovoAmbiente();
        var fato = NovoFato();

        ambiente.ClienteFake.EnfileirarFalha(new HttpRequestException("timeout simulado"));

        var resultado = await ambiente.Narrador.NarrarAsync([fato]);

        var insight = Assert.Single(resultado);
        Assert.Equal(ConsultorNarracaoOrigem.Template, insight.Origem);
        Assert.Equal(fato.TemplateFallback, insight.Frase);
    }

    [Fact]
    public async Task NarrarAsync_ComMesmosFatos_SegundaChamadaNaoInvocaOpenAiDeNovo()
    {
        var ambiente = NovoAmbiente();
        var fato = NovoFato();

        ambiente.ClienteFake.EnfileirarResposta(
            RespostaJson((fato.RuleId, "Seu caixa aguenta cerca de 45 dias no ritmo atual.")));

        var primeiraChamada = await ambiente.Narrador.NarrarAsync([fato]);
        var segundaChamada = await ambiente.Narrador.NarrarAsync([fato]);

        Assert.Equal(1, ambiente.ClienteFake.Chamadas);
        Assert.Equal(primeiraChamada[0].Frase, segundaChamada[0].Frase);
        Assert.Equal(ConsultorNarracaoOrigem.Llm, segundaChamada[0].Origem);
    }

    [Fact]
    public async Task NarrarAsync_SemApiKey_UsaTemplateSemChamarOpenAi()
    {
        var ambiente = NovoAmbiente(apiKey: null);
        var fato = NovoFato();

        var resultado = await ambiente.Narrador.NarrarAsync([fato]);

        var insight = Assert.Single(resultado);
        Assert.Equal(ConsultorNarracaoOrigem.Template, insight.Origem);
        Assert.Equal(fato.TemplateFallback, insight.Frase);
        Assert.Equal(0, ambiente.ClienteFake.Chamadas);
    }

    [Fact]
    public async Task NarrarAsync_ComOrcamentoEstourado_CaiParaTemplateSemChamarOpenAiDeNovo()
    {
        // Teto de 1 centavo — a primeira chamada (mesmo com poucos tokens) já estoura o orçamento.
        var ambiente = NovoAmbiente(orcamentoMensalCentavos: 1);
        var primeiroFato = NovoFato(ruleId: "fin.runway", diasTexto: "45");
        var segundoFato = NovoFato(ruleId: "fin.breakeven", diasTexto: "12");

        ambiente.ClienteFake.EnfileirarResposta(
            RespostaJson((primeiroFato.RuleId, "Seu caixa aguenta cerca de 45 dias no ritmo atual.")),
            promptTokens: 10_000, completionTokens: 10_000);

        var primeiroResultado = await ambiente.Narrador.NarrarAsync([primeiroFato]);
        Assert.Equal(ConsultorNarracaoOrigem.Llm, primeiroResultado[0].Origem);
        Assert.Equal(1, ambiente.ClienteFake.Chamadas);

        // Fato DIFERENTE (hash diferente, sem cache) — orçamento já estourado pela chamada anterior,
        // não deveria nem tentar chamar a OpenAI de novo (a fila do fake está vazia, então chamar de
        // novo lançaria e o teste falharia diferente do esperado se o guard não funcionasse).
        var segundoResultado = await ambiente.Narrador.NarrarAsync([segundoFato]);

        Assert.Equal(ConsultorNarracaoOrigem.Template, segundoResultado[0].Origem);
        Assert.Equal(segundoFato.TemplateFallback, segundoResultado[0].Frase);
        Assert.Equal(1, ambiente.ClienteFake.Chamadas);
    }
}
