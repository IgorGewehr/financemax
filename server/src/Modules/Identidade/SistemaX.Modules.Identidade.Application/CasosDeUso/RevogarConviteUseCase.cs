using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record RevogarConviteComando(string BusinessId, string ConviteId);

/// <summary><c>POST /api/convites/{id}/revogar</c> — mesmo guard de permissão de
/// <see cref="CriarConviteUseCase"/>. Falha se o convite já foi aceito (ver
/// <see cref="Convite.Revogar"/>); idempotente se já estava revogado.</summary>
public sealed class RevogarConviteUseCase(IConviteRepository convites, IRelogio relogio)
{
    public static readonly Error NaoEncontrado = new("identidade.convite.nao_encontrado", "Convite não encontrado.");

    public async Task<Result<Convite>> ExecutarAsync(RevogarConviteComando comando, CancellationToken ct = default)
    {
        var convite = await convites.ObterPorIdAsync(comando.BusinessId, comando.ConviteId, ct).ConfigureAwait(false);
        if (convite is null)
        {
            return Result.Falhar<Convite>(NaoEncontrado);
        }

        var revogado = convite.Revogar(relogio.Agora());
        if (revogado.Falha)
        {
            return Result.Falhar<Convite>(revogado.Erro);
        }

        await convites.SalvarAsync(convite, ct).ConfigureAwait(false);
        return Result.Ok(convite);
    }
}
