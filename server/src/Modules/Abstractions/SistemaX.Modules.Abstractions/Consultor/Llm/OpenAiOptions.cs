using Microsoft.Extensions.Configuration;

namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// Configuração do narrador via LLM — resolvida UMA VEZ no boot (mesmo padrão de
/// <c>JwtAuthSetup.ResolverOptions</c>: <c>Environment.GetEnvironmentVariable</c> primeiro,
/// <see cref="IConfiguration"/> como fallback, nunca hardcoded). <see cref="ApiKey"/> ausente é um
/// estado VÁLIDO (ao contrário de <c>FINANCEMAX_JWT_SECRET</c>) — <see cref="NarradorLlm"/>
/// degrada para <see cref="NarradorTemplate"/> sem lançar, o servidor sobe normalmente sem a chave
/// configurada (ver tarefa do Super Consultor: "SE OPENAI_API_KEY ausente → usa só o template").
/// </summary>
public sealed record OpenAiOptions(string? ApiKey, string Modelo, long OrcamentoMensalCentavos)
{
    /// <summary>R$20/mês por instalação (F1 é single-tenant fixo — ver <c>FinancemaxHost</c>) —
    /// teto conservador o suficiente para nunca surpreender o dono com uma fatura da OpenAI, e
    /// generoso o suficiente para o volume de insights do Super Consultor (poucas dezenas de
    /// fatos/dia, cache por hash evita rechamada quando nada mudou).</summary>
    public const long OrcamentoMensalPadraoCentavos = 2_000;

    public const string ModeloPadrao = "gpt-4o-mini";

    public static OpenAiOptions Resolver(IConfiguration configuracao)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? configuracao["OpenAi:ApiKey"];
        var modelo = Environment.GetEnvironmentVariable("OPENAI_MODEL_DEFAULT")
            ?? configuracao["OpenAi:Modelo"]
            ?? ModeloPadrao;

        var tetoTexto = Environment.GetEnvironmentVariable("OPENAI_ORCAMENTO_MENSAL_CENTAVOS")
            ?? configuracao["OpenAi:OrcamentoMensalCentavos"];
        var teto = long.TryParse(tetoTexto, out var valor) && valor > 0 ? valor : OrcamentoMensalPadraoCentavos;

        return new OpenAiOptions(string.IsNullOrWhiteSpace(apiKey) ? null : apiKey, modelo, teto);
    }
}
