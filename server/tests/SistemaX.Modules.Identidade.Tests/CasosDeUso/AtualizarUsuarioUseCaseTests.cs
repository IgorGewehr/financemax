using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class AtualizarUsuarioUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    private static async Task<(AtualizarUsuarioUseCase UseCase, InMemoryUsuarioRepository Usuarios, Usuario Founder)> NovoCenarioComUmFounderAsync()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var refresh = new InMemoryRefreshTokenRepository();
        var relogio = new FakeRelogio(Agora);

        var founder = Usuario.Criar(BusinessId, "Dono", "dono@exemplo.com", SenhaHasher.Hash("Senha!Forte9x"), Papel.Founder, Agora).Valor;
        await usuarios.SalvarAsync(founder);

        return (new AtualizarUsuarioUseCase(usuarios, refresh, relogio), usuarios, founder);
    }

    [Fact]
    public async Task Nao_permite_rebaixar_o_ultimo_founder_ativo()
    {
        var (useCase, _, founder) = await NovoCenarioComUmFounderAsync();

        var resultado = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, founder.Id, NovoPapel: Papel.Admin));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.usuario.ultimo_founder", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Nao_permite_desativar_o_ultimo_founder_ativo()
    {
        var (useCase, _, founder) = await NovoCenarioComUmFounderAsync();

        var resultado = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, founder.Id, Ativo: false));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.usuario.ultimo_founder", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Permite_rebaixar_um_founder_quando_existe_outro_founder_ativo()
    {
        var (useCase, usuarios, founder1) = await NovoCenarioComUmFounderAsync();
        var founder2 = Usuario.Criar(BusinessId, "Sócio", "socio@exemplo.com", SenhaHasher.Hash("Senha!Forte9x"), Papel.Founder, Agora).Valor;
        await usuarios.SalvarAsync(founder2);

        var resultado = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, founder1.Id, NovoPapel: Papel.Admin));

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Admin, resultado.Valor.Papel);
    }

    [Fact]
    public async Task Permite_rebaixar_um_admin_livremente()
    {
        var (useCase, usuarios, _) = await NovoCenarioComUmFounderAsync();
        var admin = Usuario.Criar(BusinessId, "Gerente", "gerente@exemplo.com", SenhaHasher.Hash("Senha!Forte9x"), Papel.Admin, Agora).Valor;
        await usuarios.SalvarAsync(admin);

        var resultado = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, admin.Id, NovoPapel: Papel.Viewer));

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Viewer, resultado.Valor.Papel);
    }

    [Fact]
    public async Task Usuario_nao_encontrado_falha()
    {
        var (useCase, _, _) = await NovoCenarioComUmFounderAsync();

        var resultado = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, "id-que-nao-existe", Ativo: true));

        Assert.True(resultado.Falha);
        Assert.Equal(AtualizarUsuarioUseCase.NaoEncontrado.Codigo, resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Reset_de_senha_marca_mustChangePassword_e_recusa_senha_fraca()
    {
        var (useCase, usuarios, founder2) = await NovoCenarioComUmFounderAsync();
        var alvo = Usuario.Criar(BusinessId, "Colega", "colega@exemplo.com", SenhaHasher.Hash("Senha!Forte9x"), Papel.Viewer, Agora).Valor;
        await usuarios.SalvarAsync(alvo);

        var fraca = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, alvo.Id, ResetarSenhaPara: "12345678"));
        Assert.True(fraca.Falha);

        var forte = await useCase.ExecutarAsync(new AtualizarUsuarioComando(BusinessId, alvo.Id, ResetarSenhaPara: "N0v4Senha!Forte"));
        Assert.True(forte.Sucesso);
        Assert.True(forte.Valor.MustChangePassword);
    }
}
