using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.RefreshTokens;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.CasosDeUso;

public sealed record LoginComando(string BusinessId, string Email, string Senha);

public sealed record TokensEmitidosResultado(
    Usuario Usuario, string AccessToken, DateTimeOffset AccessTokenExpiraEm, string RefreshToken, DateTimeOffset RefreshTokenExpiraEm);

/// <summary>
/// <c>POST /api/auth/login</c> (§4 do escopo F2). Mesmo erro/código para "e-mail não existe" e
/// "senha errada" (§7: "erros de auth não vazam se o e-mail existe") — inclusive o TEMPO de
/// resposta: quando o e-mail não é encontrado, ainda assim roda um Argon2 contra um hash-isca
/// fixo, pra não dar ao atacante um oráculo de timing barato pra enumerar contas.
/// </summary>
public sealed class LoginUseCase(
    IUsuarioRepository usuarios, IRefreshTokenRepository refreshTokens, ITentativaLoginStore tentativas,
    GeradorDeTokens geradorDeTokens, IRelogio relogio)
{
    /// <summary>Hash Argon2id de um placeholder fixo — só para "queimar" o mesmo tempo de CPU de
    /// uma verificação real quando o e-mail não existe (nunca comparado contra segredo nenhum).</summary>
    private static readonly string HashIsca = SenhaHasher.Hash("hash-isca-tempo-constante-login");

    public static readonly Error CredenciaisInvalidas = new("identidade.login.credenciais_invalidas", "E-mail ou senha inválidos.");

    public async Task<Result<TokensEmitidosResultado>> ExecutarAsync(LoginComando comando, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var emailNormalizado = Usuario.NormalizarEmail(comando.Email);
        if (emailNormalizado is null)
        {
            return Result.Falhar<TokensEmitidosResultado>(CredenciaisInvalidas);
        }

        var chaveLockout = $"{comando.BusinessId}:{emailNormalizado}";
        var status = tentativas.Verificar(chaveLockout, agora);
        if (status.Bloqueado)
        {
            return Result.Falhar<TokensEmitidosResultado>(new Error(
                "identidade.login.bloqueado_temporariamente",
                $"Muitas tentativas — tente novamente após {status.BloqueadoAte:HH:mm:ss}."));
        }

        var usuario = await usuarios.ObterPorEmailAsync(comando.BusinessId, emailNormalizado, ct).ConfigureAwait(false);

        if (usuario is null || !usuario.Ativo)
        {
            SenhaHasher.Verificar(comando.Senha, HashIsca); // queima tempo — ver comentário da classe
            tentativas.RegistrarFalha(chaveLockout, agora);
            return Result.Falhar<TokensEmitidosResultado>(CredenciaisInvalidas);
        }

        if (!SenhaHasher.Verificar(comando.Senha, usuario.SenhaHash))
        {
            tentativas.RegistrarFalha(chaveLockout, agora);
            return Result.Falhar<TokensEmitidosResultado>(CredenciaisInvalidas);
        }

        tentativas.RegistrarSucesso(chaveLockout);

        var (resultado, _) = await EmitirParAsync(usuario, agora, ct).ConfigureAwait(false);
        return Result.Ok(resultado);
    }

    /// <summary>Emite um novo par access+refresh e devolve também o <c>Id</c> do registro de
    /// refresh recém-criado — <see cref="RefreshTokenUseCase"/> usa esse id para apontar o
    /// registro ANTIGO como "substituído por" (cadeia de rotação), sem precisar de um segundo
    /// lookup por hash.</summary>
    internal async Task<(TokensEmitidosResultado Resultado, string NovoRefreshRegistroId)> EmitirParAsync(
        Usuario usuario, DateTimeOffset agora, CancellationToken ct)
    {
        var (accessToken, accessExpiraEm) = geradorDeTokens.GerarAccessToken(usuario, agora);
        var refresh = geradorDeTokens.GerarRefreshToken(agora);

        var registro = RefreshTokenRegistro.Emitir(usuario.BusinessId, usuario.Id, refresh.Hash, agora, refresh.ExpiraEm - agora);
        await refreshTokens.SalvarAsync(registro, ct).ConfigureAwait(false);

        return (new TokensEmitidosResultado(usuario, accessToken, accessExpiraEm, refresh.Bruto, refresh.ExpiraEm), registro.Id);
    }
}
