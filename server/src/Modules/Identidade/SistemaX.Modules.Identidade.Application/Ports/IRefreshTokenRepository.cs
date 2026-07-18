using SistemaX.Modules.Identidade.Domain.RefreshTokens;

namespace SistemaX.Modules.Identidade.Application.Ports;

/// <summary>Port de <see cref="RefreshTokenRegistro"/>. Lookup é sempre por HASH (nunca pelo
/// bruto — o bruto não é persistido, ver comentário do agregado).</summary>
public interface IRefreshTokenRepository
{
    Task<RefreshTokenRegistro?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default);

    Task SalvarAsync(RefreshTokenRegistro registro, CancellationToken ct = default);

    /// <summary>Revoga TODOS os refresh tokens ainda ativos do usuário — usado na DETECÇÃO DE
    /// REUSO (um token já revogado reapresentado ⇒ possível roubo; reação é derrubar a sessão
    /// inteira, não só o token replayado) e no reset administrativo de senha.</summary>
    Task RevogarTodosDoUsuarioAsync(string businessId, string usuarioId, DateTimeOffset agora, CancellationToken ct = default);
}
