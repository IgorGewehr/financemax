using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record CriarConviteComando(string BusinessId, string CriadoPorUsuarioId, string Email, Papel Papel);

/// <summary>O <see cref="TokenBruto"/> só existe AQUI — a resposta de <c>POST /api/convites</c> é a
/// única vez que ele sai do servidor (o front monta o link <c>/aceitar-convite?token=...</c> e
/// repassa ao convidado; nada além disso persiste o valor bruto, ver <see cref="Convite"/>).</summary>
public sealed record ConviteEmitido(Convite Convite, string TokenBruto);

/// <summary><c>POST /api/convites</c> — só founder/admin chega aqui
/// (<c>RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios)</c> no endpoint, mesmo guard de
/// <c>POST /api/usuarios</c>). Recusa convidar um e-mail que já é usuário do negócio — convite
/// nunca é o caminho para "editar" alguém que já existe.</summary>
public sealed class CriarConviteUseCase(IConviteRepository convites, IUsuarioRepository usuarios, IRelogio relogio)
{
    private static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    public static readonly Error EmailInvalido = new("identidade.convite.email_invalido", "E-mail inválido.");
    public static readonly Error EmailJaCadastrado = new("identidade.convite.email_ja_cadastrado", "Já existe um usuário com este e-mail.");

    public async Task<Result<ConviteEmitido>> ExecutarAsync(CriarConviteComando comando, CancellationToken ct = default)
    {
        var emailNormalizado = Usuario.NormalizarEmail(comando.Email);
        if (emailNormalizado is null)
        {
            return Result.Falhar<ConviteEmitido>(EmailInvalido);
        }

        var usuarioExistente = await usuarios.ObterPorEmailAsync(comando.BusinessId, emailNormalizado, ct).ConfigureAwait(false);
        if (usuarioExistente is not null)
        {
            return Result.Falhar<ConviteEmitido>(EmailJaCadastrado);
        }

        var agora = relogio.Agora();
        var (bruto, hash) = ConviteTokenGerador.Gerar();

        var criado = Convite.Criar(comando.BusinessId, emailNormalizado, comando.Papel, hash, comando.CriadoPorUsuarioId, agora, Validade);
        if (criado.Falha)
        {
            return Result.Falhar<ConviteEmitido>(criado.Erro);
        }

        await convites.SalvarAsync(criado.Valor, ct).ConfigureAwait(false);
        return Result.Ok(new ConviteEmitido(criado.Valor, bruto));
    }
}
