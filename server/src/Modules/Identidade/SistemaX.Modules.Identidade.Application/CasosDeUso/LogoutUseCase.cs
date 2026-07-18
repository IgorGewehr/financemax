using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record LogoutComando(string RefreshTokenBruto);

/// <summary><c>POST /api/auth/logout</c> (§4) — revoga o refresh apresentado. IDEMPOTENTE e
/// deliberadamente NUNCA falha por "token não encontrado/já revogado": do ponto de vista do
/// cliente, o objetivo (não estar mais logado com este token) já está satisfeito.</summary>
public sealed class LogoutUseCase(IRefreshTokenRepository refreshTokens, IRelogio relogio)
{
    public async Task<Result> ExecutarAsync(LogoutComando comando, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comando.RefreshTokenBruto))
        {
            return Result.Ok();
        }

        var hash = GeradorDeTokens.HashDoRefreshToken(comando.RefreshTokenBruto);
        var registro = await refreshTokens.ObterPorHashAsync(hash, ct).ConfigureAwait(false);
        if (registro is null || !registro.Ativo)
        {
            return Result.Ok();
        }

        registro.Revogar(relogio.Agora());
        await refreshTokens.SalvarAsync(registro, ct).ConfigureAwait(false);

        return Result.Ok();
    }
}
