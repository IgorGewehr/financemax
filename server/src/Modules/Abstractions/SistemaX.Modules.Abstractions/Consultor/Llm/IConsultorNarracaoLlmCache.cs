namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// Cache do narrador LLM, chaveado por (módulo, rule, hash dos fatos) — item 3 da tarefa do Super
/// Consultor ("CACHE por sha256(facts): se os facts do período não mudaram, reusa a narração, não
/// chama a OpenAI de novo").
///
/// DELIBERADAMENTE separado de <see cref="IConsultorInsightCache"/> (o cache que
/// <c>ConsultorService</c> já usa upstream, keyed por <c>businessId</c>): <see cref="IConsultorNarrador.NarrarAsync"/>
/// não recebe <c>businessId</c> (contrato existente, não alterado por esta rodada — F1 é
/// single-tenant fixo, ver <see cref="IConsultorOrcamentoLlm"/>), então este cache não teria como
/// se chavear por tenant mesmo se quisesse. Ele existe para <see cref="NarradorLlm"/> ser
/// AUTOSSUFICIENTE (correto mesmo chamado direto, fora do gate de <c>ConsultorService</c>) — na
/// prática, em produção, <c>ConsultorService</c> já filtra pra só os fatos que MUDARAM antes de
/// chamar o narrador, então este cache aqui é o cinto-e-suspensório que faz o requisito "2ª
/// chamada com os mesmos fatos não invoca a OpenAI de novo" valer também quando testado/usado
/// isoladamente.
/// </summary>
public interface IConsultorNarracaoLlmCache
{
    ConsultorInsightNarrado? ObterSeAtual(string modulo, string ruleId, string factsHash);

    void Gravar(string modulo, string ruleId, string factsHash, ConsultorInsightNarrado insight);
}
