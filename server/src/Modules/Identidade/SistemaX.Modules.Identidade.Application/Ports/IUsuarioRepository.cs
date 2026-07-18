using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Application.Ports;

/// <summary>Port do agregado <see cref="Usuario"/> — mesmo molde de
/// <c>IAporteDeCapitalRepository</c> (Financeiro): tenant SEMPRE explícito no parâmetro (R1),
/// nunca implícito.</summary>
public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(string businessId, string usuarioId, CancellationToken ct = default);

    /// <summary><paramref name="email"/> já deve vir normalizado
    /// (<see cref="Usuario.NormalizarEmail"/>) — o port não normaliza, o chamador (caso de uso)
    /// normaliza uma vez e reusa para checar unicidade e para o lookup de login.</summary>
    Task<Usuario?> ObterPorEmailAsync(string businessId, string email, CancellationToken ct = default);

    Task<IReadOnlyList<Usuario>> ListarAsync(string businessId, CancellationToken ct = default);

    Task SalvarAsync(Usuario usuario, CancellationToken ct = default);

    /// <summary>Quantos usuários ATIVOS com papel Founder existem no negócio — base do invariante
    /// "nunca rebaixar/desativar o último founder" (§4 do escopo F2). <paramref name="excetoId"/>
    /// exclui o próprio usuário sendo avaliado da contagem (pergunta é sempre "sobra algum OUTRO
    /// founder além deste?").</summary>
    Task<int> ContarFoundersAtivosAsync(string businessId, string? excetoId = null, CancellationToken ct = default);
}
