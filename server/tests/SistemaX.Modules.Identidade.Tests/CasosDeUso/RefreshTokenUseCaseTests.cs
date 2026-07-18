using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class RefreshTokenUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly JwtOptions Jwt = new(new string('s', 40));

    private sealed record Cenario(
        LoginUseCase LoginUseCase, RefreshTokenUseCase RefreshUseCase, InMemoryRefreshTokenRepository Refresh, FakeRelogio Relogio, Usuario Usuario);

    private static async Task<Cenario> NovoCenarioAsync()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var refresh = new InMemoryRefreshTokenRepository();
        var tentativas = new InMemoryTentativaLoginStore();
        var relogio = new FakeRelogio(Agora);
        var gerador = new GeradorDeTokens(Jwt);

        var usuario = Usuario.Criar(BusinessId, "Dono", "dono@exemplo.com", SenhaHasher.Hash("Senha!Forte9x"), Papel.Founder, Agora).Valor;
        await usuarios.SalvarAsync(usuario);

        var loginUseCase = new LoginUseCase(usuarios, refresh, tentativas, gerador, relogio);
        var refreshUseCase = new RefreshTokenUseCase(usuarios, refresh, loginUseCase, relogio);
        return new Cenario(loginUseCase, refreshUseCase, refresh, relogio, usuario);
    }

    [Fact]
    public async Task Refresh_valido_emite_par_novo_e_revoga_o_antigo()
    {
        var cenario = await NovoCenarioAsync();
        var login = await cenario.LoginUseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        var tokenAntigo = login.Valor.RefreshToken;

        var refresh = await cenario.RefreshUseCase.ExecutarAsync(new RefreshTokenComando(tokenAntigo));

        Assert.True(refresh.Sucesso);
        Assert.NotEqual(tokenAntigo, refresh.Valor.RefreshToken);

        var registroAntigo = await cenario.Refresh.ObterPorHashAsync(GeradorDeTokens.HashDoRefreshToken(tokenAntigo));
        Assert.False(registroAntigo!.Ativo);
    }

    [Fact]
    public async Task Refresh_com_token_ja_usado_reutilizado_falha_e_revoga_toda_a_cadeia()
    {
        var cenario = await NovoCenarioAsync();
        var login = await cenario.LoginUseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));
        var tokenOriginal = login.Valor.RefreshToken;

        var primeiroRefresh = await cenario.RefreshUseCase.ExecutarAsync(new RefreshTokenComando(tokenOriginal));
        Assert.True(primeiroRefresh.Sucesso);
        var tokenNovo = primeiroRefresh.Valor.RefreshToken;

        // Reapresenta o token ORIGINAL (já rotacionado/revogado) — replay de um token roubado.
        var reuso = await cenario.RefreshUseCase.ExecutarAsync(new RefreshTokenComando(tokenOriginal));

        Assert.True(reuso.Falha);
        Assert.Equal(RefreshTokenUseCase.TokenReutilizado.Codigo, reuso.Erro.Codigo);

        // A cadeia inteira (inclusive o token NOVO, legítimo) foi revogada por segurança.
        var tentativaComTokenNovo = await cenario.RefreshUseCase.ExecutarAsync(new RefreshTokenComando(tokenNovo));
        Assert.True(tentativaComTokenNovo.Falha);
    }

    [Fact]
    public async Task Refresh_expirado_falha()
    {
        var cenario = await NovoCenarioAsync();
        var login = await cenario.LoginUseCase.ExecutarAsync(new LoginComando(BusinessId, "dono@exemplo.com", "Senha!Forte9x"));

        cenario.Relogio.Momento = Agora.AddDays(31); // além dos 30 dias de validade padrão

        var resultado = await cenario.RefreshUseCase.ExecutarAsync(new RefreshTokenComando(login.Valor.RefreshToken));

        Assert.True(resultado.Falha);
        Assert.Equal(RefreshTokenUseCase.TokenExpirado.Codigo, resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Refresh_com_token_inexistente_falha()
    {
        var cenario = await NovoCenarioAsync();

        var resultado = await cenario.RefreshUseCase.ExecutarAsync(new RefreshTokenComando("token-que-nunca-existiu"));

        Assert.True(resultado.Falha);
        Assert.Equal(RefreshTokenUseCase.TokenInvalido.Codigo, resultado.Erro.Codigo);
    }
}
