using System.Collections.Concurrent;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;

namespace SistemaX.Modules.Identidade.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IConviteRepository"/> — mesmo molde de
/// <c>InMemoryUsuarioRepository</c>/<c>InMemoryRefreshTokenRepository</c>.</summary>
public sealed class InMemoryConviteRepository : IConviteRepository
{
    private readonly ConcurrentDictionary<string, Convite> _porId = new();

    public Task<Convite?> ObterPorIdAsync(string businessId, string conviteId, CancellationToken ct = default)
    {
        var convite = _porId.GetValueOrDefault(conviteId);
        return Task.FromResult(convite is not null && convite.BusinessId == businessId ? convite : null);
    }

    public Task<Convite?> ObterPorTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(c => c.TokenHash == tokenHash));

    public Task<IReadOnlyList<Convite>> ListarPendentesAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Convite>>(_porId.Values
            .Where(c => c.BusinessId == businessId && c.AceitoEm is null && c.RevogadoEm is null)
            .OrderByDescending(c => c.CriadoEm)
            .ToList());

    public Task SalvarAsync(Convite convite, CancellationToken ct = default)
    {
        _porId[convite.Id] = convite;
        return Task.CompletedTask;
    }
}
