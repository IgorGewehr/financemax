using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.Autorizacao;

/// <summary>
/// Invariante "nunca rebaixar/desativar o último founder" (§4 do escopo F2) — sem isto, um
/// admin(bug/engano) consegue deixar o negócio SEM NINGUÉM capaz de gerenciar usuários
/// (<c>PapelHierarquia.PodeAdministrarUsuarios</c> exige Founder/Admin, mas só Founder tem TODAS
/// as permissões — um negócio sem founder ativo ainda funciona, mas nunca mais consegue promover
/// outro founder sozinho). Checa ANTES de aplicar a mudança — puramente consultivo, quem aplica é
/// <c>AtualizarUsuarioUseCase</c>.
/// </summary>
public static class UltimoFounderGuard
{
    public static async Task<Result> ExigirNaoEhUltimoFounderAtivoAsync(
        IUsuarioRepository repositorio, Usuario alvo, CancellationToken ct = default)
    {
        // Só é uma operação sensível se o ALVO está, agora, ocupando a vaga de founder ativo —
        // rebaixar um Admin ou desativar um Viewer nunca aciona este guard.
        if (alvo.Papel != Papel.Founder || !alvo.Ativo)
        {
            return Result.Ok();
        }

        var outrosFoundersAtivos = await repositorio
            .ContarFoundersAtivosAsync(alvo.BusinessId, excetoId: alvo.Id, ct)
            .ConfigureAwait(false);

        return outrosFoundersAtivos > 0
            ? Result.Ok()
            : Result.Falhar(new Error(
                "identidade.usuario.ultimo_founder",
                "Não é possível rebaixar/desativar o último founder ativo do negócio."));
    }
}
