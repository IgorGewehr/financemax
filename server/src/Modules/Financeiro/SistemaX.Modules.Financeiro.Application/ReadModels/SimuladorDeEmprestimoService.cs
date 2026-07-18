using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>
/// Simulador de empréstimo (Bancário) — <c>POST /financeiro/bancario/simular-emprestimo</c>. Read
/// model puro de ORQUESTRAÇÃO: zero fórmula própria (tudo em <see cref="SimulacaoDeEmprestimo"/>,
/// § "o lar único de cada fórmula"), só busca a FOLGA mensal real do negócio e repassa pro motor
/// quant.
///
/// FOLGA MENSAL: <see cref="DreGerencialService.ResultadoOperacional"/> dos últimos 30 dias corridos
/// até hoje — o MESMO lucro operacional que já alimenta o DRE (<c>/financeiro/relatorios/dre</c>) e
/// a MC%/PE (<see cref="PontoDeEquilibrioService.CalcularMargemContribuicaoPercentualAsync"/> usa a
/// mesma janela de 30 dias) — nunca uma segunda conta de "quanto sobra por mês". Negativa quando o
/// negócio está no vermelho no período: repassada como está (não floramos em zero) — é exatamente o
/// dado que faz <see cref="SimulacaoDeEmprestimo"/> apontar <c>Inviável</c> quando não há retorno de
/// equipamento informado que cubra o rombo.
/// </summary>
public sealed class SimuladorDeEmprestimoService(DreGerencialService dreGerencial, IRelogio relogio)
{
    private const int DiasDeJanelaParaFolga = 30;

    public async Task<SimulacaoDeEmprestimo.Resultado> SimularAsync(
        string businessId, long valorCentavos, int taxaJurosMensalBps, int prazoMeses,
        long? retornoMensalEsperadoCentavos = null, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(relogio.Agora().UtcDateTime);
        var inicioJanela = new DateTimeOffset(hoje.AddDays(-DiasDeJanelaParaFolga).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var fimJanela = new DateTimeOffset(hoje.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var dre = await dreGerencial.CalcularAsync(businessId, inicioJanela, fimJanela, ct).ConfigureAwait(false);
        var folgaMensalCentavos = dre.ResultadoOperacional.Centavos;

        return SimulacaoDeEmprestimo.Calcular(
            valorCentavos, taxaJurosMensalBps, prazoMeses, folgaMensalCentavos, retornoMensalEsperadoCentavos);
    }
}
