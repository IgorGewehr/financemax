using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

/// <summary>Contract test do port <see cref="IConviteRepository"/> — roda 2× (InMemory + SQLite),
/// mesmo molde de <c>UsuarioRepositoryContractTests</c>.</summary>
public abstract class ConviteRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IConviteRepository CriarRepositorio();

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    private static Convite NovoConvite(
        string businessId, string email = "convidado@exemplo.com", string tokenHash = "hash-fake",
        Papel papel = Papel.Operator, TimeSpan? validade = null)
        => Convite.Criar(businessId, email, papel, tokenHash, "founder-1", Agora, validade ?? TimeSpan.FromDays(7)).Valor;

    [Fact]
    public async Task Salvar_e_obter_por_id_retorna_o_mesmo_convite()
    {
        var repo = CriarRepositorio();
        var convite = NovoConvite(BusinessA);

        await repo.SalvarAsync(convite);
        var lido = await repo.ObterPorIdAsync(BusinessA, convite.Id);

        Assert.NotNull(lido);
        Assert.Equal(convite.Id, lido!.Id);
        Assert.Equal("convidado@exemplo.com", lido.Email);
        Assert.Equal(Papel.Operator, lido.Papel);
        Assert.Equal("founder-1", lido.CriadoPorUsuarioId);
        Assert.Null(lido.AceitoEm);
        Assert.Null(lido.RevogadoEm);
    }

    [Fact]
    public async Task Obter_por_id_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var convite = NovoConvite(BusinessA);
        await repo.SalvarAsync(convite);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, convite.Id));
    }

    [Fact]
    public async Task ObterPorTokenHashAsync_encontra_independente_do_business()
    {
        var repo = CriarRepositorio();
        var convite = NovoConvite(BusinessA, tokenHash: "hash-unico-achavel");
        await repo.SalvarAsync(convite);

        var achado = await repo.ObterPorTokenHashAsync("hash-unico-achavel");
        var naoAchado = await repo.ObterPorTokenHashAsync("hash-que-nao-existe");

        Assert.NotNull(achado);
        Assert.Equal(convite.Id, achado!.Id);
        Assert.Null(naoAchado);
    }

    [Fact]
    public async Task ListarPendentesAsync_so_do_tenant_e_so_nao_resolvidos()
    {
        var repo = CriarRepositorio();

        var pendente = NovoConvite(BusinessA, email: "pendente@exemplo.com", tokenHash: "hash-pendente");

        var aceito = NovoConvite(BusinessA, email: "aceito@exemplo.com", tokenHash: "hash-aceito");
        aceito.Aceitar(Agora.AddMinutes(1));

        var revogado = NovoConvite(BusinessA, email: "revogado@exemplo.com", tokenHash: "hash-revogado");
        revogado.Revogar(Agora.AddMinutes(1));

        var outroTenant = NovoConvite(BusinessB, email: "outro@exemplo.com", tokenHash: "hash-outro-tenant");

        await repo.SalvarAsync(pendente);
        await repo.SalvarAsync(aceito);
        await repo.SalvarAsync(revogado);
        await repo.SalvarAsync(outroTenant);

        var lista = await repo.ListarPendentesAsync(BusinessA);

        Assert.Single(lista);
        Assert.Equal(pendente.Id, lista[0].Id);
    }

    [Fact]
    public async Task ListarPendentesAsync_inclui_expirado_mas_nao_resolvido()
    {
        var repo = CriarRepositorio();
        var expirado = NovoConvite(BusinessA, tokenHash: "hash-expirado", validade: TimeSpan.FromSeconds(1));
        await repo.SalvarAsync(expirado);

        var lista = await repo.ListarPendentesAsync(BusinessA);

        Assert.Single(lista);
        Assert.Equal(StatusConvite.Expirado, lista[0].Status(Agora.AddDays(1)));
    }

    [Fact]
    public async Task SalvarAsync_de_novo_atualiza_o_status_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        var convite = NovoConvite(BusinessA);
        await repo.SalvarAsync(convite);

        convite.Aceitar(Agora.AddMinutes(5));
        await repo.SalvarAsync(convite);

        var lido = await repo.ObterPorIdAsync(BusinessA, convite.Id);
        Assert.NotNull(lido!.AceitoEm);

        var lista = await repo.ListarPendentesAsync(BusinessA);
        Assert.Empty(lista);
    }
}
