using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record CriarUsuarioComando(string BusinessId, string Nome, string Email, string Senha, Papel Papel);

/// <summary><c>POST /api/usuarios</c> (§4) — só admin/founder chega aqui
/// (<c>RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios)</c> no endpoint). E-mail
/// único POR NEGÓCIO (R1 — não há checagem global, só dentro do <c>businessId</c> do chamador).</summary>
public sealed class CriarUsuarioUseCase(IUsuarioRepository usuarios, IRelogio relogio)
{
    public async Task<Result<Usuario>> ExecutarAsync(CriarUsuarioComando comando, CancellationToken ct = default)
    {
        var emailNormalizado = Usuario.NormalizarEmail(comando.Email);
        if (emailNormalizado is null)
        {
            return Result.Falhar<Usuario>(new Error("identidade.usuario.email_invalido", "E-mail inválido."));
        }

        var politica = PoliticaDeSenha.Validar(comando.Senha, comando.Nome, emailNormalizado);
        if (politica.Falha)
        {
            return Result.Falhar<Usuario>(politica.Erro);
        }

        var existente = await usuarios.ObterPorEmailAsync(comando.BusinessId, emailNormalizado, ct).ConfigureAwait(false);
        if (existente is not null)
        {
            return Result.Falhar<Usuario>(new Error("identidade.usuario.email_em_uso", "Já existe um usuário com este e-mail."));
        }

        var agora = relogio.Agora();
        var senhaHash = SenhaHasher.Hash(comando.Senha);

        var criado = Usuario.Criar(comando.BusinessId, comando.Nome, emailNormalizado, senhaHash, comando.Papel, agora);
        if (criado.Falha)
        {
            return criado;
        }

        await usuarios.SalvarAsync(criado.Valor, ct).ConfigureAwait(false);
        return criado;
    }
}
