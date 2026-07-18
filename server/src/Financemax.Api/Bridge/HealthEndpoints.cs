namespace Financemax.Api.Bridge;

/// <summary>Endpoint do PRÓPRIO Host, fora de qualquer IModule — espelha <c>BridgeEndpoints.Mapear</c>
/// do Host.Desktop, reduzido ao health check (F1 não tem PIN/boot-token; auth real é F2). Anônimo de
/// propósito: sonda de vida para orquestração Docker/Compose, não deve exigir sessão.</summary>
public static class HealthEndpoints
{
    public static void Mapear(IEndpointRouteBuilder app, DateTimeOffset iniciadoEm)
    {
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            servico = "financemax-api",
            uptimeSegundos = (long)(DateTimeOffset.UtcNow - iniciadoEm).TotalSeconds,
        })).AllowAnonymous();
    }
}
