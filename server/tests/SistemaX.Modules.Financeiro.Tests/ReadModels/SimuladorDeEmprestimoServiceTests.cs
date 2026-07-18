using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// SimuladorDeEmprestimoService é só ORQUESTRAÇÃO: busca a folga mensal real (DreGerencialService.
/// ResultadoOperacional dos últimos 30 dias) e repassa pro motor quant puro (já coberto termo a
/// termo por <c>SimulacaoDeEmprestimoTests</c>). Estes testes cobrem só o fio: "a folga que chega no
/// veredito é mesmo a do DRE do período certo", não a matemática da viabilidade em si.
/// </summary>
public sealed class SimuladorDeEmprestimoServiceTests
{
    private const string BusinessId = "biz-simulador-emprestimo";

    private sealed record Ambiente(SimuladorDeEmprestimoService Servico, InMemoryContaAReceberRepository ContasAReceber, FakeRelogio Relogio);

    private static Ambiente NovoAmbiente(DateTimeOffset hoje)
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();
        var ativosDeCapital = new InMemoryAtivoDeCapitalRepository();
        var relogio = new FakeRelogio(hoje);

        var dre = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis, ativosDeCapital);
        var servico = new SimuladorDeEmprestimoService(dre, relogio);

        return new Ambiente(servico, contasAReceber, relogio);
    }

    [Fact]
    public async Task SimularAsync_NegocioComFolgaFolgada_UsaOResultadoOperacionalDoDreComoFolgaEDaViavel()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // Receita reconhecida sem custo direto (categoria "Serviços", sem comissão/CMV) -> vira
        // ResultadoOperacional = 5.000, dentro dos últimos 30 dias.
        var receita = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "r1"), "Serviço", CategoriaFinanceiraPadrao.Servicos,
            hoje.AddDays(-5), Money.DeReais(5_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(5_000), hoje.AddDays(-5))).Valor;
        await ambiente.ContasAReceber.SalvarAsync(receita);

        // Parcela ≈ R$945,60 (10.000 @ 2%/12m) contra folga de 5.000 -> ~18,9%, folgada -> Viável.
        var resultado = await ambiente.Servico.SimularAsync(
            BusinessId, valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12);

        Assert.Equal(945_60, resultado.ParcelaCentavos);
        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Viavel, resultado.Viabilidade.Veredito);
    }

    [Fact]
    public async Task SimularAsync_SemNenhumaReceitaNoPeriodo_FolgaZeroDaInviavel()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var resultado = await ambiente.Servico.SimularAsync(
            BusinessId, valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Inviavel, resultado.Viabilidade.Veredito);
    }

    [Fact]
    public async Task SimularAsync_ReceitaForaDaJanelaDeTrintaDias_NaoEntraNaFolga()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        // 60 dias atrás -> fora da janela de 30 dias usada pra folga -> não conta.
        var receitaAntiga = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "r-antiga"), "Serviço", CategoriaFinanceiraPadrao.Servicos,
            hoje.AddDays(-60), Money.DeReais(50_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(50_000), hoje.AddDays(-60))).Valor;
        await ambiente.ContasAReceber.SalvarAsync(receitaAntiga);

        var resultado = await ambiente.Servico.SimularAsync(
            BusinessId, valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Inviavel, resultado.Viabilidade.Veredito);
    }

    [Fact]
    public async Task SimularAsync_ComRetornoEsperadoDoEquipamento_RepassaParaOMotorQuant()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var resultado = await ambiente.Servico.SimularAsync(
            BusinessId, valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12,
            retornoMensalEsperadoCentavos: 1_000_00);

        // valor=12.000, retorno=1.000/mês -> payback de 12 meses, sem depender da folga.
        Assert.Equal(12, resultado.Viabilidade.PaybackMeses);
    }
}
