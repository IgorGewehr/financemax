using SistemaX.Modules.Abstractions.Consultor.Llm;

namespace SistemaX.Modules.Financeiro.Tests.Fakes;

/// <summary>Fake do cliente OpenAI — a suíte de <c>NarradorLlm</c> NUNCA chama a API real. Devolve
/// uma resposta pré-programada por chamada (fila) ou lança a exceção programada; conta chamadas
/// pra provar cache/orçamento (item "2ª chamada com mesmos fatos não chama o fake de novo").</summary>
public sealed class FakeOpenAiChatClient : IOpenAiChatClient
{
    private readonly Queue<Func<OpenAiChatResultado>> _respostas = new();

    public int Chamadas { get; private set; }
    public string? UltimoPromptUsuario { get; private set; }

    public void EnfileirarResposta(string conteudoJson, int promptTokens = 100, int completionTokens = 50) =>
        _respostas.Enqueue(() => new OpenAiChatResultado(conteudoJson, promptTokens, completionTokens));

    public void EnfileirarFalha(Exception excecao) =>
        _respostas.Enqueue(() => throw excecao);

    public Task<OpenAiChatResultado> ChatAsync(
        string promptSistema, string promptUsuario, string modelo, CancellationToken ct = default)
    {
        Chamadas++;
        UltimoPromptUsuario = promptUsuario;

        if (_respostas.Count == 0)
        {
            throw new InvalidOperationException("FakeOpenAiChatClient: nenhuma resposta enfileirada para esta chamada.");
        }

        return Task.FromResult(_respostas.Dequeue()());
    }
}
