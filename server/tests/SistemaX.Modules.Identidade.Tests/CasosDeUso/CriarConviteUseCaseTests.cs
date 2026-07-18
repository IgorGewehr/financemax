using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class CriarConviteUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Founder_cria_convite_com_papel_founder_para_um_socio()
    {
        var convites = new InMemoryConviteRepository();
        var usuarios = new InMemoryUsuarioRepository();
        var useCase = new CriarConviteUseCase(convites, usuarios, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new CriarConviteComando(BusinessId, "founder-1", "socio@exemplo.com", Papel.Founder));

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Founder, resultado.Valor.Convite.Papel);
        Assert.Equal("socio@exemplo.com", resultado.Valor.Convite.Email);
        Assert.NotEmpty(resultado.Valor.TokenBruto);

        var pendentes = await convites.ListarPendentesAsync(BusinessId);
        Assert.Single(pendentes);
    }

    [Fact]
    public async Task Recusa_convidar_email_que_ja_e_usuario_do_negocio()
    {
        var convites = new InMemoryConviteRepository();
        var usuarios = new InMemoryUsuarioRepository();
        var useCase = new CriarConviteUseCase(convites, usuarios, new FakeRelogio(Agora));

        await usuarios.SalvarAsync(SistemaX.Modules.Identidade.Domain.Usuarios.Usuario.Criar(
            BusinessId, "Já Existe", "existente@exemplo.com", "hash-fake", Papel.Viewer, Agora).Valor);

        var resultado = await useCase.ExecutarAsync(new CriarConviteComando(BusinessId, "founder-1", "existente@exemplo.com", Papel.Operator));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.convite.email_ja_cadastrado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Recusa_email_invalido()
    {
        var convites = new InMemoryConviteRepository();
        var usuarios = new InMemoryUsuarioRepository();
        var useCase = new CriarConviteUseCase(convites, usuarios, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new CriarConviteComando(BusinessId, "founder-1", "nao-e-email", Papel.Operator));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.convite.email_invalido", resultado.Erro.Codigo);
    }
}
