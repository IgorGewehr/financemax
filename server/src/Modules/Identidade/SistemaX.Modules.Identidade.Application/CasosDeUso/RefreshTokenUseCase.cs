using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record RefreshTokenComando(string RefreshTokenBruto);

/// <summary>
/// <c>POST /api/auth/refresh</c> (§4 do escopo F2) — ROTACIONA: emite um par novo e revoga o
/// antigo (aponta pro sucessor). Se o token apresentado já estava revogado (reuso de um token
/// roubado, replay depois da rotação legítima), reage revogando a CADEIA INTEIRA do usuário —
/// mais seguro forçar um novo login do que deixar o token comprometido continuar valendo.
/// </summary>
public sealed class RefreshTokenUseCase(
    IUsuarioRepository usuarios, IRefreshTokenRepository refreshTokens, LoginUseCase loginUseCase, IRelogio relogio)
{
    public static readonly Error TokenInvalido = new("identidade.refresh.invalido", "Refresh token inválido.");
    public static readonly Error TokenReutilizado = new("identidade.refresh.reutilizado", "Refresh token já utilizado — sessão revogada por segurança.");
    public static readonly Error TokenExpirado = new("identidade.refresh.expirado", "Refresh token expirado — faça login novamente.");

    public async Task<Result<TokensEmitidosResultado>> ExecutarAsync(RefreshTokenComando comando, CancellationToken ct = default)
    {
        var agora = relogio.Agora();

        if (string.IsNullOrWhiteSpace(comando.RefreshTokenBruto))
        {
            return Result.Falhar<TokensEmitidosResultado>(TokenInvalido);
        }

        var hash = GeradorDeTokens.HashDoRefreshToken(comando.RefreshTokenBruto);
        var registro = await refreshTokens.ObterPorHashAsync(hash, ct).ConfigureAwait(false);
        if (registro is null)
        {
            return Result.Falhar<TokensEmitidosResultado>(TokenInvalido);
        }

        if (!registro.Ativo)
        {
            // Detecção de reuso — o registro já foi revogado (por rotação, logout ou reset) e
            // está sendo apresentado de novo. Derruba toda a cadeia do usuário.
            await refreshTokens.RevogarTodosDoUsuarioAsync(registro.BusinessId, registro.UsuarioId, agora, ct).ConfigureAwait(false);
            return Result.Falhar<TokensEmitidosResultado>(TokenReutilizado);
        }

        if (registro.Expirado(agora))
        {
            return Result.Falhar<TokensEmitidosResultado>(TokenExpirado);
        }

        var usuario = await usuarios.ObterPorIdAsync(registro.BusinessId, registro.UsuarioId, ct).ConfigureAwait(false);
        if (usuario is null || !usuario.Ativo)
        {
            return Result.Falhar<TokensEmitidosResultado>(TokenInvalido);
        }

        var (novoPar, novoRegistroId) = await loginUseCase.EmitirParAsync(usuario, agora, ct).ConfigureAwait(false);

        registro.RevogarPorRotacao(novoRegistroId, agora);
        await refreshTokens.SalvarAsync(registro, ct).ConfigureAwait(false);

        return Result.Ok(novoPar);
    }
}
