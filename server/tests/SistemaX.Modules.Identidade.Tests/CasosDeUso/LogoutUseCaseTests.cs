using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class LogoutUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly JwtOptions Jwt = new(new string('s', 40));

    [Fact]
    public async Task Logout_revoga_o_refresh_token_apresentado()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var refresh = new InMemoryRefreshTokenRepository();
        var tentativas = new InMemoryTentativaLoginStore();
        var relogio = new FakeRelogio(Agora);
        var gerador = new GeradorDeTokens(Jwt);

        var usuario = Usuario.Criar(BusinessId, "Dono", "dono@exemplo.com", SenhaHasher.Hash("Senha!Forte9x"), Papel.Founder, Agora).Valor;
        await usuarios.SalvarAsync(usuario);

        var loginUseCase = new LoginUseCase(usuarios, refresh, tentativas, gerador, relogio);
        var logoutUseCase = new LogoutUseCase(refresh, relogio);
        var refreshUseCase = new RefreshTokenUseCase(usuarios, refresh, loginUseCase, relogio);

        var login = await loginUseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        var token = login.Valor.RefreshToken;

        var logout = await logoutUseCase.ExecutarAsync(new LogoutComando(token));
        Assert.True(logout.Sucesso);

        var tentativaDeRefresh = await refreshUseCase.ExecutarAsync(new RefreshTokenComando(token));
        Assert.True(tentativaDeRefresh.Falha);
    }

    [Fact]
    public async Task Logout_e_idempotente_token_desconhecido_nao_falha()
    {
        var refresh = new InMemoryRefreshTokenRepository();
        var logoutUseCase = new LogoutUseCase(refresh, new FakeRelogio(Agora));

        var primeira = await logoutUseCase.ExecutarAsync(new LogoutComando("token-nunca-existiu"));
        var segunda = await logoutUseCase.ExecutarAsync(new LogoutComando("token-nunca-existiu"));

        Assert.True(primeira.Sucesso);
        Assert.True(segunda.Sucesso);
    }
}
