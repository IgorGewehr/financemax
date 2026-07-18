using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

/// <summary><see cref="Motivo"/> só é preenchido quando <see cref="Valido"/> é <c>false</c> —
/// <c>"nao_encontrado"</c>/<c>"aceito"</c>/<c>"revogado"</c>/<c>"expirado"</c>. NUNCA carrega mais
/// que <see cref="Email"/>/<see cref="Papel"/> do convite (nunca quem criou, nunca o token) — é a
/// tela de pré-cadastro ANÔNIMA, só pode devolver o mínimo pro front pré-preencher o formulário.</summary>
public sealed record ConsultaConviteResultado(bool Valido, string? Email, string? Papel, string? Motivo);

/// <summary><c>GET /api/convites/{token}</c> (ANÔNIMO) — pré-preenche a tela de aceite do convite
/// antes do convidado escolher a própria senha em <c>POST /api/auth/registrar</c>.</summary>
public sealed class ConsultarConvitePorTokenUseCase(IConviteRepository convites, IRelogio relogio)
{
    public async Task<ConsultaConviteResultado> ExecutarAsync(string tokenBruto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenBruto))
        {
            return new ConsultaConviteResultado(false, null, null, "nao_encontrado");
        }

        var hash = ConviteTokenGerador.Hash(tokenBruto);
        var convite = await convites.ObterPorTokenHashAsync(hash, ct).ConfigureAwait(false);
        if (convite is null)
        {
            return new ConsultaConviteResultado(false, null, null, "nao_encontrado");
        }

        var papel = convite.Papel.ToString();
        return convite.Status(relogio.Agora()) switch
        {
            StatusConvite.Pendente => new ConsultaConviteResultado(true, convite.Email, papel, null),
            StatusConvite.Aceito => new ConsultaConviteResultado(false, convite.Email, papel, "aceito"),
            StatusConvite.Revogado => new ConsultaConviteResultado(false, convite.Email, papel, "revogado"),
            _ => new ConsultaConviteResultado(false, convite.Email, papel, "expirado"),
        };
    }
}
