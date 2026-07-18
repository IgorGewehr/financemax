using System.Collections.Concurrent;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.RefreshTokens;

namespace SistemaX.Modules.Identidade.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IRefreshTokenRepository"/>.</summary>
public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<string, RefreshTokenRegistro> _porId = new();

    public Task<RefreshTokenRegistro?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(r => r.TokenHash == tokenHash));

    public Task SalvarAsync(RefreshTokenRegistro registro, CancellationToken ct = default)
    {
        _porId[registro.Id] = registro;
        return Task.CompletedTask;
    }

    public Task RevogarTodosDoUsuarioAsync(string businessId, string usuarioId, DateTimeOffset agora, CancellationToken ct = default)
    {
        foreach (var registro in _porId.Values.Where(r => r.BusinessId == businessId && r.UsuarioId == usuarioId && r.Ativo))
        {
            registro.Revogar(agora);
        }

        return Task.CompletedTask;
    }
}
