using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

/// <summary>Contract test do port <see cref="IUsuarioRepository"/> — roda 2× (InMemory + SQLite),
/// mesmo molde de <c>AporteDeCapitalRepositoryContractTests</c> (Financeiro).</summary>
public abstract class UsuarioRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IUsuarioRepository CriarRepositorio();

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    private static Usuario NovoUsuario(
        string businessId, string email = "dono@exemplo.com", Papel papel = Papel.Founder, bool ativo = true)
        => Usuario.Criar(businessId, "Dono", email, "hash-fake", papel, Agora, ativo).Valor;

    [Fact]
    public async Task Salvar_e_obter_por_id_retorna_o_mesmo_usuario()
    {
        var repo = CriarRepositorio();
        var usuario = NovoUsuario(BusinessA);

        await repo.SalvarAsync(usuario);
        var lido = await repo.ObterPorIdAsync(BusinessA, usuario.Id);

        Assert.NotNull(lido);
        Assert.Equal(usuario.Id, lido!.Id);
        Assert.Equal("dono@exemplo.com", lido.Email);
        Assert.Equal(Papel.Founder, lido.Papel);
        Assert.True(lido.Ativo);
    }

    [Fact]
    public async Task Obter_por_id_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var usuario = NovoUsuario(BusinessA);
        await repo.SalvarAsync(usuario);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, usuario.Id));
    }

    [Fact]
    public async Task ObterPorEmailAsync_encontra_e_e_filtrado_por_business()
    {
        var repo = CriarRepositorio();
        var usuario = NovoUsuario(BusinessA, email: "achavel@exemplo.com");
        await repo.SalvarAsync(usuario);

        var achado = await repo.ObterPorEmailAsync(BusinessA, "achavel@exemplo.com");
        var naoAchado = await repo.ObterPorEmailAsync(BusinessB, "achavel@exemplo.com");

        Assert.NotNull(achado);
        Assert.Equal(usuario.Id, achado!.Id);
        Assert.Null(naoAchado);
    }

    [Fact]
    public async Task ListarAsync_retorna_so_do_tenant()
    {
        var repo = CriarRepositorio();
        var a1 = NovoUsuario(BusinessA, email: "a1@exemplo.com");
        var a2 = NovoUsuario(BusinessA, email: "a2@exemplo.com");
        var b1 = NovoUsuario(BusinessB, email: "b1@exemplo.com");

        await repo.SalvarAsync(a1);
        await repo.SalvarAsync(a2);
        await repo.SalvarAsync(b1);

        var lista = await repo.ListarAsync(BusinessA);
        Assert.Equal(2, lista.Count);
        Assert.All(lista, u => Assert.Equal(BusinessA, u.BusinessId));
    }

    [Fact]
    public async Task SalvarAsync_de_novo_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        var usuario = NovoUsuario(BusinessA);
        await repo.SalvarAsync(usuario);

        usuario.Desativar(Agora.AddMinutes(5));
        await repo.SalvarAsync(usuario);

        var lido = await repo.ObterPorIdAsync(BusinessA, usuario.Id);
        Assert.False(lido!.Ativo);

        var lista = await repo.ListarAsync(BusinessA);
        Assert.Single(lista);
    }

    [Fact]
    public async Task ContarFoundersAtivosAsync_conta_so_founders_ativos_do_tenant()
    {
        var repo = CriarRepositorio();
        var founderAtivo1 = NovoUsuario(BusinessA, email: "f1@exemplo.com", papel: Papel.Founder, ativo: true);
        var founderAtivo2 = NovoUsuario(BusinessA, email: "f2@exemplo.com", papel: Papel.Founder, ativo: true);
        var founderInativo = NovoUsuario(BusinessA, email: "f3@exemplo.com", papel: Papel.Founder, ativo: false);
        var admin = NovoUsuario(BusinessA, email: "adm@exemplo.com", papel: Papel.Admin, ativo: true);
        var founderOutroTenant = NovoUsuario(BusinessB, email: "f-outro@exemplo.com", papel: Papel.Founder, ativo: true);

        await repo.SalvarAsync(founderAtivo1);
        await repo.SalvarAsync(founderAtivo2);
        await repo.SalvarAsync(founderInativo);
        await repo.SalvarAsync(admin);
        await repo.SalvarAsync(founderOutroTenant);

        Assert.Equal(2, await repo.ContarFoundersAtivosAsync(BusinessA));
        Assert.Equal(1, await repo.ContarFoundersAtivosAsync(BusinessA, excetoId: founderAtivo1.Id));
    }
}
