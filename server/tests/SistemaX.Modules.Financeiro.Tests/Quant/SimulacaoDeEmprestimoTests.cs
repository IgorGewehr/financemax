using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class SimulacaoDeEmprestimoTests
{
    // ─────────────────────────────────────────────────────────────────────────────────────────
    // PMT (Tabela Price) — PMT = P·i/(1-(1+i)^-n)
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_R10Mil2PctAoMes12Meses_ParcelaBateOExemploClassicoDeTabelaPrice()
    {
        // R$10.000, 2%/mês, 12 meses -> parcela ≈ R$945,60 (exemplo clássico de Tabela Price).
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Equal(945_60, resultado.ParcelaCentavos);
    }

    [Fact]
    public void Calcular_TaxaZero_ParcelaEValorDivididoPeloPrazo()
    {
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Equal(1_000_00, resultado.ParcelaCentavos);
        Assert.Equal(0, resultado.JurosTotalCentavos);
    }

    [Fact]
    public void Calcular_CustoTotalEJurosTotal_DerivamDaParcelaVezesPrazoMenosOPrincipal()
    {
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Equal(resultado.ParcelaCentavos * 12, resultado.CustoTotalCentavos);
        Assert.Equal(resultado.CustoTotalCentavos - 10_000_00, resultado.JurosTotalCentavos);
        Assert.True(resultado.JurosTotalCentavos > 0);
    }

    [Fact]
    public void Calcular_TaxaEfetivaAnual_EhACapitalizacaoCompostaMensalMenosUm()
    {
        // i=2%/mês -> (1.02)^12 - 1 ≈ 26,8242% a.a. -> 2682 bps (arredondado).
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Equal(2682, resultado.TaxaEfetivaAnualBps);
    }

    [Fact]
    public void Calcular_TaxaZero_TaxaEfetivaAnualEhZero()
    {
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 0, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Equal(0, resultado.TaxaEfetivaAnualBps);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Viabilidade — o coração do simulador: parcela vs. folga mensal real, +retorno opcional.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ParcelaCabeConfortavelNaFolga_EhViavel()
    {
        // parcela ≈ 945,60; folga = 10.000 -> ~9,5% da folga, bem abaixo do piso de 50%.
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Viavel, resultado.Viabilidade.Veredito);
        Assert.True(resultado.Viabilidade.ParcelaVsFolgaPercent < 50);
        Assert.Contains("viável", resultado.Viabilidade.Motivo);
    }

    [Fact]
    public void Calcular_ParcelaMaiorQueAFolgaESemRetorno_EhInviavel()
    {
        // parcela=1000/mês (taxa 0, valor 12000/12), folga=500 -> folga efetiva = 500-1000 = -500 < 0.
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12, folgaMensalCentavos: 500_00);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Inviavel, resultado.Viabilidade.Veredito);
        Assert.Equal(200.0, resultado.Viabilidade.ParcelaVsFolgaPercent);
        Assert.Contains("inviável", resultado.Viabilidade.Motivo);
    }

    [Fact]
    public void Calcular_ParcelaCabeMasConsomeMaisDeMetadeDaFolga_EhApertado()
    {
        // parcela=1000/mês, folga=1500 -> 66,7% da folga (>50%), folga efetiva=500 >= 0.
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12, folgaMensalCentavos: 1_500_00);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Apertado, resultado.Viabilidade.Veredito);
        Assert.True(resultado.Viabilidade.ParcelaVsFolgaPercent > 50);
    }

    [Fact]
    public void Calcular_RetornoDoEquipamentoCobreAParcela_ViraViavelMesmoComPercentAltoDaFolgaBruta()
    {
        // parcela=1000/mês, folga=1500 (66,7%, seria Apertado sozinho), mas retorno=1200 >= parcela
        // -> a dívida se autofinancia; folga efetiva = 1500+1200-1000 = 1700 >= 0.
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12,
            folgaMensalCentavos: 1_500_00, retornoMensalEsperadoCentavos: 1_200_00);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Viavel, resultado.Viabilidade.Veredito);
        Assert.Contains("retorna", resultado.Viabilidade.Motivo);
        Assert.Contains("sobra", resultado.Viabilidade.Motivo);
    }

    [Fact]
    public void Calcular_RetornoNaoCobreOSuficiente_AindaFicaInviavelSeFolgaEfetivaNegativa()
    {
        // parcela=1000/mês, folga=0, retorno=400 -> folga efetiva = 0+400-1000 = -600 < 0.
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12,
            folgaMensalCentavos: 0, retornoMensalEsperadoCentavos: 400_00);

        Assert.Equal(SimulacaoDeEmprestimo.VeredictoViabilidade.Inviavel, resultado.Viabilidade.Veredito);
    }

    [Fact]
    public void Calcular_FolgaMensalZeroOuNegativa_ParcelaVsFolgaPercentUsaSentinelaFinita()
    {
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12, folgaMensalCentavos: -500_00);

        Assert.Equal(999.9, resultado.Viabilidade.ParcelaVsFolgaPercent);
        Assert.True(double.IsFinite(resultado.Viabilidade.ParcelaVsFolgaPercent));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Payback do equipamento (§ classe) — valor ÷ retorno mensal, arredondado pra cima.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ComRetornoInformado_PaybackMesesEhValorDivididoPeloRetornoArredondadoParaCima()
    {
        // valor=12.000, retorno=1.000/mês -> exatamente 12 meses.
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 12_000_00, taxaJurosMensalBps: 0, prazoMeses: 12,
            folgaMensalCentavos: 10_000_00, retornoMensalEsperadoCentavos: 1_000_00);

        Assert.Equal(12, resultado.Viabilidade.PaybackMeses);
    }

    [Fact]
    public void Calcular_PaybackNaoDivideExato_ArredondaParaCima()
    {
        // valor=10.000, retorno=3.000/mês -> 3,33... -> 4 meses (arredonda pra cima).
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 0, prazoMeses: 12,
            folgaMensalCentavos: 10_000_00, retornoMensalEsperadoCentavos: 3_000_00);

        Assert.Equal(4, resultado.Viabilidade.PaybackMeses);
    }

    [Fact]
    public void Calcular_SemRetornoInformado_PaybackMesesEhNull()
    {
        var resultado = SimulacaoDeEmprestimo.Calcular(
            valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 12, folgaMensalCentavos: 10_000_00);

        Assert.Null(resultado.Viabilidade.PaybackMeses);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Validação de entrada
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_ValorZeroOuNegativo_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulacaoDeEmprestimo.Calcular(valorCentavos: 0, taxaJurosMensalBps: 200, prazoMeses: 12, folgaMensalCentavos: 1_000_00));
    }

    [Fact]
    public void Calcular_PrazoZeroOuNegativo_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulacaoDeEmprestimo.Calcular(valorCentavos: 10_000_00, taxaJurosMensalBps: 200, prazoMeses: 0, folgaMensalCentavos: 1_000_00));
    }

    [Fact]
    public void Calcular_TaxaNegativa_LancaExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulacaoDeEmprestimo.Calcular(valorCentavos: 10_000_00, taxaJurosMensalBps: -1, prazoMeses: 12, folgaMensalCentavos: 1_000_00));
    }
}
