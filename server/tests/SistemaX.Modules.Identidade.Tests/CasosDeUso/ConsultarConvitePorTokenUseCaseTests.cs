using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Convites;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class ConsultarConvitePorTokenUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Token_pendente_pre_preenche_email_e_papel()
    {
        var convites = new InMemoryConviteRepository();
        var relogio = new FakeRelogio(Agora);
        var (bruto, hash) = ConviteTokenGerador.Gerar();
        await convites.SalvarAsync(Convite.Criar(BusinessId, "convidado@exemplo.com", Papel.Manager, hash, "founder-1", Agora, TimeSpan.FromDays(7)).Valor);

        var useCase = new ConsultarConvitePorTokenUseCase(convites, relogio);
        var resultado = await useCase.ExecutarAsync(bruto);

        Assert.True(resultado.Valido);
        Assert.Equal("convidado@exemplo.com", resultado.Email);
        Assert.Equal("Manager", resultado.Papel);
        Assert.Null(resultado.Motivo);
    }

    [Fact]
    public async Task Token_inexistente_devolve_invalido_sem_vazar_dado()
    {
        var convites = new InMemoryConviteRepository();
        var useCase = new ConsultarConvitePorTokenUseCase(convites, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync("token-que-nao-existe");

        Assert.False(resultado.Valido);
        Assert.Null(resultado.Email);
        Assert.Equal("nao_encontrado", resultado.Motivo);
    }

    [Fact]
    public async Task Token_expirado_devolve_invalido_com_motivo_expirado()
    {
        var convites = new InMemoryConviteRepository();
        var relogio = new FakeRelogio(Agora);
        var (bruto, hash) = ConviteTokenGerador.Gerar();
        await convites.SalvarAsync(Convite.Criar(BusinessId, "convidado@exemplo.com", Papel.Manager, hash, "founder-1", Agora.AddDays(-8), TimeSpan.FromDays(7)).Valor);

        var useCase = new ConsultarConvitePorTokenUseCase(convites, relogio);
        var resultado = await useCase.ExecutarAsync(bruto);

        Assert.False(resultado.Valido);
        Assert.Equal("expirado", resultado.Motivo);
    }

    [Fact]
    public async Task Token_ja_aceito_devolve_invalido_com_motivo_aceito()
    {
        var convites = new InMemoryConviteRepository();
        var relogio = new FakeRelogio(Agora);
        var (bruto, hash) = ConviteTokenGerador.Gerar();
        var convite = Convite.Criar(BusinessId, "convidado@exemplo.com", Papel.Manager, hash, "founder-1", Agora, TimeSpan.FromDays(7)).Valor;
        convite.Aceitar(Agora.AddMinutes(1));
        await convites.SalvarAsync(convite);

        var useCase = new ConsultarConvitePorTokenUseCase(convites, relogio);
        var resultado = await useCase.ExecutarAsync(bruto);

        Assert.False(resultado.Valido);
        Assert.Equal("aceito", resultado.Motivo);
    }
}
