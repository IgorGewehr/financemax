namespace SistemaX.Modules.Identidade.Application.Auth;

/// <summary>
/// Configuração de emissão/validação de JWT (§3 do escopo F2). <see cref="ChaveSecreta"/> vem
/// SEMPRE de config/env (<c>FINANCEMAX_JWT_SECRET</c>, ver <c>Financemax.Api/Program.cs</c>) —
/// nunca hardcoded (regra dura do prompt). Access token curto (~15 min) + refresh rotativo
/// (~30 dias) — o desenho padrão de sessão web moderna (curto = raio de dano de um access token
/// vazado é pequeno; refresh rotativo = revogável e detecta reuso).
/// </summary>
public sealed record JwtOptions(
    string ChaveSecreta,
    string Emissor = "financemax-api",
    string Audiencia = "financemax-app",
    int AccessTokenMinutos = 15,
    int RefreshTokenDias = 30)
{
    public TimeSpan AccessTokenValidade => TimeSpan.FromMinutes(AccessTokenMinutos);
    public TimeSpan RefreshTokenValidade => TimeSpan.FromDays(RefreshTokenDias);

    /// <summary>Claim type usado para carregar o businessId no token — <c>SessaoClaimsMiddleware</c>
    /// (Financemax.Api) lê esta MESMA constante para popular <c>HttpContext.Items</c>.</summary>
    public const string ClaimBusinessId = "businessId";

    /// <summary>Claim type usado para carregar o papel (RBAC) no token.</summary>
    public const string ClaimPapel = "papel";
}
