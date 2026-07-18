using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Domain.Convites;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class RevogarConviteUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Revoga_um_convite_pendente()
    {
        var convites = new InMemoryConviteRepository();
        var relogio = new FakeRelogio(Agora);
        var convite = Convite.Criar(BusinessId, "convidado@exemplo.com", Papel.Operator, "hash", "founder-1", Agora, TimeSpan.FromDays(7)).Valor;
        await convites.SalvarAsync(convite);

        var useCase = new RevogarConviteUseCase(convites, relogio);
        var resultado = await useCase.ExecutarAsync(new RevogarConviteComando(BusinessId, convite.Id));

        Assert.True(resultado.Sucesso);
        Assert.NotNull(resultado.Valor.RevogadoEm);

        var pendentes = await convites.ListarPendentesAsync(BusinessId);
        Assert.Empty(pendentes);
    }

    [Fact]
    public async Task Convite_inexistente_devolve_nao_encontrado()
    {
        var convites = new InMemoryConviteRepository();
        var useCase = new RevogarConviteUseCase(convites, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new RevogarConviteComando(BusinessId, "id-que-nao-existe"));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.convite.nao_encontrado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Convite_ja_aceito_nao_pode_ser_revogado()
    {
        var convites = new InMemoryConviteRepository();
        var relogio = new FakeRelogio(Agora);
        var convite = Convite.Criar(BusinessId, "convidado@exemplo.com", Papel.Operator, "hash", "founder-1", Agora, TimeSpan.FromDays(7)).Valor;
        convite.Aceitar(Agora.AddMinutes(1));
        await convites.SalvarAsync(convite);

        var useCase = new RevogarConviteUseCase(convites, relogio);
        var resultado = await useCase.ExecutarAsync(new RevogarConviteComando(BusinessId, convite.Id));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.convite.ja_aceito", resultado.Erro.Codigo);
    }
}
