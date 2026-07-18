using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class LoginUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly JwtOptions Jwt = new(new string('s', 40));

    private sealed record Cenario(LoginUseCase UseCase, InMemoryUsuarioRepository Usuarios, InMemoryRefreshTokenRepository Refresh, FakeRelogio Relogio);

    private static async Task<Cenario> NovoCenarioComUsuarioAsync(string senha = "Senha!Forte9x", bool ativo = true)
    {
        var usuarios = new InMemoryUsuarioRepository();
        var refresh = new InMemoryRefreshTokenRepository();
        var tentativas = new InMemoryTentativaLoginStore();
        var relogio = new FakeRelogio(Agora);
        var gerador = new GeradorDeTokens(Jwt);

        var usuario = Usuario.Criar(BusinessId, "Dono", "dono@exemplo.com", SenhaHasher.Hash(senha), Papel.Founder, Agora, ativo).Valor;
        await usuarios.SalvarAsync(usuario);

        var useCase = new LoginUseCase(usuarios, refresh, tentativas, gerador, relogio);
        return new Cenario(useCase, usuarios, refresh, relogio);
    }

    [Fact]
    public async Task Login_com_credenciais_corretas_emite_tokens()
    {
        var cenario = await NovoCenarioComUsuarioAsync();

        var resultado = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));

        Assert.True(resultado.Sucesso);
        Assert.NotEmpty(resultado.Valor.AccessToken);
        Assert.NotEmpty(resultado.Valor.RefreshToken);
        Assert.Equal("dono@exemplo.com", resultado.Valor.Usuario.Email);
    }

    [Fact]
    public async Task Login_com_senha_errada_falha_401_generico()
    {
        var cenario = await NovoCenarioComUsuarioAsync();

        var resultado = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "senha-errada-qualquer"));

        Assert.True(resultado.Falha);
        Assert.Equal(LoginUseCase.CredenciaisInvalidas.Codigo, resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Login_com_email_inexistente_falha_com_o_MESMO_codigo_de_senha_errada_nao_vaza_existencia()
    {
        var cenario = await NovoCenarioComUsuarioAsync();

        var resultado = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "nao-existe@exemplo.com", "qualquer-coisa"));

        Assert.True(resultado.Falha);
        Assert.Equal(LoginUseCase.CredenciaisInvalidas.Codigo, resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Login_de_usuario_inativo_falha_com_o_mesmo_codigo_generico()
    {
        var cenario = await NovoCenarioComUsuarioAsync(ativo: false);

        var resultado = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));

        Assert.True(resultado.Falha);
        Assert.Equal(LoginUseCase.CredenciaisInvalidas.Codigo, resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Apos_N_falhas_consecutivas_a_conta_fica_bloqueada_temporariamente()
    {
        var cenario = await NovoCenarioComUsuarioAsync();

        Result<TokensEmitidosResultado>? ultimoResultado = null;
        for (var i = 0; i < 6; i++)
        {
            ultimoResultado = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "senha-errada"));
        }

        Assert.True(ultimoResultado!.Falha);
        Assert.Equal("identidade.login.bloqueado_temporariamente", ultimoResultado.Erro.Codigo);

        // Mesmo com a senha CORRETA agora, o bloqueio ainda vale (a janela não expirou).
        var comSenhaCerta = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        Assert.True(comSenhaCerta.Falha);
        Assert.Equal("identidade.login.bloqueado_temporariamente", comSenhaCerta.Erro.Codigo);
    }

    [Fact]
    public async Task Apos_bloqueio_expirar_login_correto_volta_a_funcionar()
    {
        var cenario = await NovoCenarioComUsuarioAsync();

        for (var i = 0; i < 6; i++)
        {
            await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "senha-errada"));
        }

        cenario.Relogio.Momento = Agora.AddMinutes(20); // além do teto de bloqueio (15 min)

        var resultado = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public async Task Login_bem_sucedido_reseta_o_contador_de_falhas()
    {
        var cenario = await NovoCenarioComUsuarioAsync();

        await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "errada-1"));
        await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "errada-2"));
        var sucesso = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        Assert.True(sucesso.Sucesso);

        // Mais 4 falhas depois do sucesso não deveriam já ter acumulado com as 2 de antes.
        for (var i = 0; i < 4; i++)
        {
            await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "errada-de-novo"));
        }
        var aindaOk = await cenario.UseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        Assert.True(aindaOk.Sucesso);
    }
}
