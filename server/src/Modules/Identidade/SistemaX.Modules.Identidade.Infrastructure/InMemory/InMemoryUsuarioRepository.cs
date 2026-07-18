using System.Collections.Concurrent;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IUsuarioRepository"/> — mesmo molde de
/// <c>InMemoryAporteDeCapitalRepository</c> (Financeiro).</summary>
public sealed class InMemoryUsuarioRepository : IUsuarioRepository
{
    private readonly ConcurrentDictionary<string, Usuario> _porId = new();

    public Task<Usuario?> ObterPorIdAsync(string businessId, string usuarioId, CancellationToken ct = default)
    {
        var usuario = _porId.GetValueOrDefault(usuarioId);
        return Task.FromResult(usuario is not null && usuario.BusinessId == businessId ? usuario : null);
    }

    public Task<Usuario?> ObterPorEmailAsync(string businessId, string email, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(u => u.BusinessId == businessId && u.Email == email));

    public Task<IReadOnlyList<Usuario>> ListarAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Usuario>>(_porId.Values
            .Where(u => u.BusinessId == businessId)
            .OrderBy(u => u.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public Task SalvarAsync(Usuario usuario, CancellationToken ct = default)
    {
        _porId[usuario.Id] = usuario;
        return Task.CompletedTask;
    }

    public Task<int> ContarFoundersAtivosAsync(string businessId, string? excetoId = null, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.Count(u =>
            u.BusinessId == businessId && u.Papel == Papel.Founder && u.Ativo && u.Id != excetoId));
}
