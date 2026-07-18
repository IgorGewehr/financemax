using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// Implementação REAL de <see cref="IOpenAiChatClient"/> — Chat Completions da OpenAI, HttpClient
/// tipado (registrado via <c>services.AddHttpClient&lt;IOpenAiChatClient, OpenAiHttpChatClient&gt;()</c>
/// no composition root, <c>FinancemaxHost</c>). <c>response_format: json_object</c> força a saída a
/// ser JSON válido — <see cref="NarradorLlm"/> depende disso para o parse em lote (um item por
/// <c>ruleId</c>), sem precisar de regex frágil sobre texto livre.
/// </summary>
public sealed class OpenAiHttpChatClient(HttpClient http, OpenAiOptions options) : IOpenAiChatClient
{
    private const string Endpoint = "https://api.openai.com/v1/chat/completions";

    public async Task<OpenAiChatResultado> ChatAsync(
        string promptSistema, string promptUsuario, string modelo, CancellationToken ct = default)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new OpenAiChatRequest(
                modelo,
                [
                    new OpenAiChatMensagem("system", promptSistema),
                    new OpenAiChatMensagem("user", promptUsuario),
                ],
                new OpenAiResponseFormat("json_object"),
                Temperatura: 0.2),
                options: JsonOpcoes),
        };

        // Bearer por requisição (via OpenAiOptions resolvido no boot, não
        // DefaultRequestHeaders do HttpClient compartilhado) — NarradorLlm só chega até aqui
        // depois de já ter checado ApiKey != null (ver NarradorLlm.NarrarAsync), mas o guard
        // abaixo é defensivo caso este cliente seja usado fora desse caminho algum dia.
        if (options.ApiKey is { Length: > 0 } chave)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);
        }

        using var resposta = await http.SendAsync(requisicao, ct).ConfigureAwait(false);
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<OpenAiChatResponse>(JsonOpcoes, ct).ConfigureAwait(false);
        var conteudo = corpo?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var promptTokens = corpo?.Usage?.PromptTokens ?? 0;
        var completionTokens = corpo?.Usage?.CompletionTokens ?? 0;

        return new OpenAiChatResultado(conteudo, promptTokens, completionTokens);
    }

    private static readonly JsonSerializerOptions JsonOpcoes = new(JsonSerializerDefaults.Web);

    private sealed record OpenAiChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenAiChatMensagem> Messages,
        [property: JsonPropertyName("response_format")] OpenAiResponseFormat ResponseFormat,
        [property: JsonPropertyName("temperature")] double Temperatura);

    private sealed record OpenAiChatMensagem(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAiResponseFormat([property: JsonPropertyName("type")] string Type);

    private sealed record OpenAiChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenAiChoice>? Choices,
        [property: JsonPropertyName("usage")] OpenAiUsage? Usage);

    private sealed record OpenAiChoice([property: JsonPropertyName("message")] OpenAiChatMensagem? Message);

    private sealed record OpenAiUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
