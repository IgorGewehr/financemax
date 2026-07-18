using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SistemaX.Modules.Identidade.Application.Auth;
using System.Threading.RateLimiting;

namespace Financemax.Api.Auth;

/// <summary>
/// Monta a autenticação JWT REAL do host (F2 — substitui o antigo <c>StubAuthMiddleware</c>).
/// <c>AddAuthentication().AddJwtBearer(...)</c> valida assinatura/emissor/audiência/expiração do
/// access token; quem faz a PONTE entre o <see cref="System.Security.Claims.ClaimsPrincipal"/>
/// resultante e o <c>HttpContext.Items</c> que o <c>RequerPermissao</c> do Financeiro já lê é
/// <see cref="SessaoClaimsMiddleware"/> — ver Program.cs para a ordem de middleware.
/// </summary>
public static class JwtAuthSetup
{
    /// <summary>
    /// Resolve a chave de assinatura JWT — SEMPRE de config/env (<c>FINANCEMAX_JWT_SECRET</c>,
    /// regra dura do escopo F2: "nunca hardcoded"). Falha RÁPIDO e alto no boot se ausente — um
    /// servidor que aceitasse subir sem segredo configurado teria, na melhor hipótese, que gerar
    /// um segredo efêmero (invalida todo refresh token a cada restart, silenciosamente) ou, na
    /// pior, cair para um valor fixo conhecido (a vulnerabilidade que a regra existe para evitar).
    /// </summary>
    public static string ResolverChaveSecreta(IConfiguration configuracao)
    {
        var segredo = Environment.GetEnvironmentVariable("FINANCEMAX_JWT_SECRET")
            ?? configuracao["Financemax:Jwt:Secret"];

        if (string.IsNullOrWhiteSpace(segredo))
        {
            throw new InvalidOperationException(
                "FINANCEMAX_JWT_SECRET não configurado — defina no .env/ambiente antes de subir o " +
                "servidor (ex.: `openssl rand -base64 48`). Nunca hardcode este valor.");
        }

        if (segredo.Length < 32)
        {
            throw new InvalidOperationException(
                $"FINANCEMAX_JWT_SECRET tem só {segredo.Length} caracteres — use ao menos 32 " +
                "(ideal: `openssl rand -base64 48`) para HMAC-SHA256 ter margem de segurança real.");
        }

        return segredo;
    }

    public static JwtOptions ResolverOptions(IConfiguration configuracao)
    {
        var chave = ResolverChaveSecreta(configuracao);

        var accessMinutos = int.TryParse(Environment.GetEnvironmentVariable("FINANCEMAX_JWT_ACCESS_MINUTOS"), out var m) ? m : 15;
        var refreshDias = int.TryParse(Environment.GetEnvironmentVariable("FINANCEMAX_JWT_REFRESH_DIAS"), out var d) ? d : 30;

        // <= 0 faria JwtSecurityToken construir com expires <= notBefore — o construtor do
        // System.IdentityModel.Tokens.Jwt LANÇA nesse caso ("Expires must be after NotBefore"),
        // ou seja um valor inválido aqui não vira token estranho, vira o servidor inteiro
        // crashando no PRIMEIRO login. Falha aqui, no boot, é preferível — mensagem clara em vez
        // de um stack trace obscuro no meio do fluxo de auth do primeiro usuário.
        if (accessMinutos <= 0)
        {
            throw new InvalidOperationException($"FINANCEMAX_JWT_ACCESS_MINUTOS deve ser > 0 (recebido: {accessMinutos}).");
        }

        if (refreshDias <= 0)
        {
            throw new InvalidOperationException($"FINANCEMAX_JWT_REFRESH_DIAS deve ser > 0 (recebido: {refreshDias}).");
        }

        return new JwtOptions(chave, AccessTokenMinutos: accessMinutos, RefreshTokenDias: refreshDias);
    }

    public static IServiceCollection AddIdentidadeAuth(this IServiceCollection services, JwtOptions jwtOptions)
    {
        services.AddSingleton(jwtOptions);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // SEM ISTO, o handler remapeia "sub" pro URI XML legado
                // (ClaimTypes.NameIdentifier) por compatibilidade histórica com WS-Federation —
                // SessaoClaimsMiddleware.FindFirst("sub") voltaria sempre null e todo endpoint
                // autenticado quebraria com "usuarioId ausente". Mantém as claims EXATAMENTE como
                // emitidas por GeradorDeTokens (sub/businessId/papel).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Emissor,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GeradorDeTokens.ChaveDeAssinatura(jwtOptions.ChaveSecreta),
                    ValidateLifetime = true,
                    // Sem tolerância extra de relógio — access token já é curto (15 min default);
                    // um clock skew de 5 min (o default do framework) daria quase 1/3 de vida a
                    // mais de graça a um token expirado. Zero é a escolha certa aqui.
                    ClockSkew = TimeSpan.Zero,
                };
            });

        services.AddAuthorization();

        // Rate limit do login (§7 — "por e-mail+IP"): particiona por IP do cliente. A parte
        // "por e-mail" da proteção é o lockout progressivo (ITentativaLoginStore, camada de
        // aplicação) — as duas defesas são complementares, não a mesma coisa: IP throttling barra
        // um único atacante batendo rápido; lockout por e-mail barra um atacante distribuído
        // (N IPs) mirando UMA conta específica. PermitLimit configurável
        // (FINANCEMAX_LOGIN_RATE_LIMIT_PERMIT) — o TestServer in-process não popula
        // RemoteIpAddress (toda requisição cai na mesma partição "sem-ip"), então a suíte de
        // integração sobe esse teto bem alto para não confundir "muitos testes" com "ataque".
        var permiteLogin = int.TryParse(Environment.GetEnvironmentVariable("FINANCEMAX_LOGIN_RATE_LIMIT_PERMIT"), out var p) ? p : 10;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth-login", http => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "sem-ip",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permiteLogin,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
