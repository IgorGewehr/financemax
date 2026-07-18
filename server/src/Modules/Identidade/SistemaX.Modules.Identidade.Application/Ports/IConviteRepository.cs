using SistemaX.Modules.Identidade.Domain.Convites;

namespace SistemaX.Modules.Identidade.Application.Ports;

/// <summary>Port do agregado <see cref="Convite"/> — mesmo molde de
/// <see cref="IUsuarioRepository"/>/<see cref="IRefreshTokenRepository"/>: tenant explícito onde faz
/// sentido (R1); <see cref="ObterPorTokenHashAsync"/> não filtra por tenant pela MESMA razão de
/// <c>IRefreshTokenRepository.ObterPorHashAsync</c> — o hash de 256 bits já é a própria chave de
/// busca, e o tenant nem é conhecido ainda no momento do lookup (o convidado bate em
/// <c>GET /api/convites/{token}</c> ANÔNIMO, antes de qualquer sessão existir).</summary>
public interface IConviteRepository
{
    Task<Convite?> ObterPorIdAsync(string businessId, string conviteId, CancellationToken ct = default);

    Task<Convite?> ObterPorTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Convites AINDA NÃO resolvidos por um admin — <c>AceitoEm IS NULL AND RevogadoEm IS
    /// NULL</c>. Inclui convites já EXPIRADOS-mas-não-revogados de propósito: a UI decide como
    /// exibi-los (ex.: badge "expirado", via <see cref="Convite.Status"/>) — esta query filtra só
    /// por "ainda não teve uma decisão administrativa", nunca por tempo.</summary>
    Task<IReadOnlyList<Convite>> ListarPendentesAsync(string businessId, CancellationToken ct = default);

    Task SalvarAsync(Convite convite, CancellationToken ct = default);
}
