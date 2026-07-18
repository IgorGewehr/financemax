namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>Resultado de uma chamada de chat completion — separado do texto cru porque
/// <see cref="NarradorLlm"/> precisa dos contadores de token para estimar custo (circuito de
/// orçamento, <see cref="IConsultorOrcamentoLlm"/>) sem precisar reabrir o JSON de resposta da
/// OpenAI de novo.</summary>
public sealed record OpenAiChatResultado(string Conteudo, int PromptTokens, int CompletionTokens);

/// <summary>
/// SEAM do cliente OpenAI — port fino o bastante para um FAKE de teste devolver texto/erro sem
/// tocar rede (a suíte de <see cref="NarradorLlm"/> NUNCA chama a API real). Implementação real:
/// <see cref="OpenAiHttpChatClient"/> (HttpClient tipado, Chat Completions).
/// </summary>
public interface IOpenAiChatClient
{
    Task<OpenAiChatResultado> ChatAsync(
        string promptSistema, string promptUsuario, string modelo, CancellationToken ct = default);
}
