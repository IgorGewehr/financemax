using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Simulador de empréstimo (Bancário) — "preciso de um empréstimo pra comprar um equipamento; vai
/// dar bom?". O diferencial deste motor sobre qualquer calculadora de PMT genérica: a viabilidade
/// não compara a parcela contra o valor do empréstimo, compara contra a FOLGA MENSAL REAL do
/// negócio (<see cref="ReadModels.DreGerencialService.ResultadoOperacional"/> dos últimos 30 dias,
/// buscado por <see cref="ReadModels.SimuladorDeEmprestimoService"/> — o mesmo lucro operacional que
/// alimenta o DRE e o Ponto de Equilíbrio, nunca uma segunda conta de "quanto sobra").
///
/// PARCELA — Tabela Price / PMT, a fórmula clássica de amortização com prestações iguais:
/// <code>PMT = P · i ÷ (1 − (1+i)^-n)</code>
/// com <c>P</c> = <paramref name="Calcular"/>.<c>valorCentavos</c>, <c>i</c> = taxa MENSAL em fração
/// (<c>taxaJurosMensalBps ÷ 10.000</c>) e <c>n</c> = <c>prazoMeses</c>. Degenera para
/// <c>P ÷ n</c> quando <c>i = 0</c> (limite de PMT quando i→0, sem juro nenhum — divisão simples do
/// principal pelas parcelas, nunca <c>0/0</c> indefinido).
///
/// CUSTO TOTAL / JUROS TOTAL: <c>CustoTotal = PMT · n</c>; <c>JurosTotal = CustoTotal − P</c> — a
/// diferença entre o que sai do caixa ao longo do contrato e o que entrou hoje.
///
/// TAXA EFETIVA ANUAL (capitalização composta mensal, a mesma conversão que a Resolução CMN usa
/// pra "custo efetivo total" simplificado — sem IOF/tarifas, só o juro):
/// <code>TaxaEfetivaAnual = (1 + i)^12 − 1</code>
///
/// VIABILIDADE — o coração do simulador. Dado a FOLGA mensal atual do negócio (<c>F</c>, ANTES do
/// empréstimo) e, opcionalmente, o RETORNO mensal esperado do equipamento (<c>R</c> — quanto ele
/// gera ou economiza por mês), definimos:
/// <list type="bullet">
/// <item><c>parcelaVsFolgaPercent = 100 · PMT ÷ F</c> — quanto da folga ATUAL (sem contar a ajuda do
/// equipamento) a parcela consome. Sentinela <c>999,9%</c> quando <c>F ≤ 0</c> (negócio já sem
/// sobra nenhuma hoje) — nunca <c>Infinity</c>/<c>NaN</c> no JSON de resposta.</item>
/// <item><c>FolgaEfetiva = F + R − PMT</c> (com <c>R = 0</c> se não informado) — o que sobra por mês
/// DEPOIS de pagar a parcela e já contando o que o equipamento traz. É esta, e não a folga bruta,
/// que decide se o empréstimo quebra o caixa.</item>
/// </list>
/// Veredito, em ordem de checagem:
/// <list type="number">
/// <item><b>Inviável</b> — <c>FolgaEfetiva &lt; 0</c>: mesmo somando o que o equipamento devolve,
/// falta caixa todo mês para pagar a parcela. Empréstimo quebra o negócio do jeito que está — reveja
/// prazo/valor.</item>
/// <item><b>Apertado</b> — <c>FolgaEfetiva ≥ 0</c> mas <c>parcelaVsFolgaPercent &gt;
/// <see cref="LimiteApertadoPercent"/></c> E o retorno do equipamento (se informado) NÃO cobre a
/// parcela sozinho (<c>R &lt; PMT</c>): cabe, mas consome mais da metade do que sobra HOJE —
/// qualquer imprevisto (queda de venda, recebível atrasado) já aperta o caixa.</item>
/// <item><b>Viável</b> — nos demais casos: ou a parcela cabe folgada na folga atual, ou o retorno do
/// equipamento cobre a parcela sozinho (a dívida vira, na prática, autofinanciada).</item>
/// </list>
/// <see cref="LimiteApertadoPercent"/> = 50%: adaptação do limite clássico de comprometimento de
/// renda (30% sobre RECEITA) para comprometimento sobre FOLGA (o que já sobra depois de toda
/// despesa) — folga é um número mais "líquido" que receita, então o piso de alerta é mais generoso;
/// acima de metade dela numa parcela só, o negócio perde a margem pra absorver qualquer solavanco.
///
/// PAYBACK DO EQUIPAMENTO: <c>PaybackMeses = ⌈valorCentavos ÷ retornoMensalEsperadoCentavos⌉</c> —
/// quantos meses o equipamento leva para devolver, em retorno/economia, o que ele custou (não o
/// custo do empréstimo — essa é uma pergunta separada, "o bem se paga?", que fica boa mesmo se o
/// financiamento fosse à vista). <c>null</c> sem retorno informado ou com retorno não-positivo (não
/// se paga nunca, então não há "quando").
/// </summary>
public static class SimulacaoDeEmprestimo
{
    /// <summary>Piso de alerta de comprometimento de folga (§ classe) — acima disso, mesmo cabendo,
    /// o veredito é <see cref="VeredictoViabilidade.Apertado"/>.</summary>
    private const double LimiteApertadoPercent = 50.0;

    /// <summary>Sentinela de <see cref="Resultado.ParcelaVsFolgaPercent"/> quando a folga mensal já é
    /// zero ou negativa (§ classe) — mantém o campo sempre um <c>double</c> finito no wire.</summary>
    private const double SentinelaSemFolga = 999.9;

    public enum VeredictoViabilidade { Viavel, Apertado, Inviavel }

    /// <param name="Veredito">Ver §-classe.</param>
    /// <param name="Motivo">Frase pronta narrando o veredito — usar isto na UI em vez de recompor
    /// texto a partir dos números crus (§-classe).</param>
    /// <param name="ParcelaVsFolgaPercent">
    /// <b>NÃO é sempre um percentual real.</b> Vale <see cref="SentinelaSemFolga"/> (999,9) quando a
    /// folga mensal do negócio já é zero/negativa hoje — um teto arbitrário só para manter o campo
    /// <c>double</c> finito no wire (nunca <c>Infinity</c>/<c>NaN</c> em JSON), NÃO "a parcela
    /// consome 999,9% da folga". Qualquer consumidor (web/relatório) que formate este número para
    /// exibição DEVE checar <paramref name="Veredito"/> == <see cref="VeredictoViabilidade.Inviavel"/>
    /// primeiro e, nesse caso, mostrar o <paramref name="Motivo"/> (já narrado por extenso) em vez do
    /// percentual bruto.
    /// </param>
    /// <param name="PaybackMeses">Meses até o equipamento se pagar sozinho — <c>null</c> sem retorno
    /// informado (§-classe).</param>
    public sealed record Viabilidade(
        VeredictoViabilidade Veredito, string Motivo, double ParcelaVsFolgaPercent, int? PaybackMeses);

    public sealed record Resultado(
        long ParcelaCentavos, long CustoTotalCentavos, long JurosTotalCentavos, int TaxaEfetivaAnualBps,
        Viabilidade Viabilidade);

    public static Resultado Calcular(
        long valorCentavos, int taxaJurosMensalBps, int prazoMeses, long folgaMensalCentavos,
        long? retornoMensalEsperadoCentavos = null)
    {
        if (valorCentavos <= 0) throw new ArgumentOutOfRangeException(nameof(valorCentavos), "Valor do empréstimo deve ser positivo.");
        if (prazoMeses <= 0) throw new ArgumentOutOfRangeException(nameof(prazoMeses), "Prazo deve ser positivo.");
        if (taxaJurosMensalBps < 0) throw new ArgumentOutOfRangeException(nameof(taxaJurosMensalBps), "Taxa de juros não pode ser negativa.");

        var i = taxaJurosMensalBps / 10_000m;
        var parcela = CalcularParcela(valorCentavos, i, prazoMeses);

        var custoTotal = parcela * prazoMeses;
        var jurosTotal = custoTotal - valorCentavos;
        var taxaEfetivaAnualBps = CalcularTaxaEfetivaAnualBps(i);

        var viabilidade = AvaliarViabilidade(parcela, folgaMensalCentavos, valorCentavos, retornoMensalEsperadoCentavos);

        return new Resultado(parcela, custoTotal, jurosTotal, taxaEfetivaAnualBps, viabilidade);
    }

    /// <summary><c>PMT = P·i/(1−(1+i)^-n)</c>; <c>i = 0 → P/n</c> (limite da fórmula quando não há
    /// juro — nunca <c>0/0</c>).</summary>
    private static long CalcularParcela(long valorCentavos, decimal i, int prazoMeses)
    {
        if (i == 0m)
            return (long)Math.Round((decimal)valorCentavos / prazoMeses, MidpointRounding.ToEven);

        var fatorDesconto = (decimal)Math.Pow(1 + (double)i, -prazoMeses);
        var denominador = 1m - fatorDesconto;
        return (long)Math.Round(valorCentavos * i / denominador, MidpointRounding.ToEven);
    }

    /// <summary><c>(1+i)^12 − 1</c>, em bps (1 bps = 0,01%).</summary>
    private static int CalcularTaxaEfetivaAnualBps(decimal i)
    {
        var fatorAnual = (decimal)Math.Pow(1 + (double)i, 12);
        return (int)Math.Round((fatorAnual - 1m) * 10_000m, MidpointRounding.ToEven);
    }

    private static Viabilidade AvaliarViabilidade(
        long parcelaCentavos, long folgaMensalCentavos, long valorCentavos, long? retornoMensalEsperadoCentavos)
    {
        var retorno = retornoMensalEsperadoCentavos ?? 0L;
        var folgaEfetivaCentavos = folgaMensalCentavos + retorno - parcelaCentavos;
        var retornoCobreAParcela = retornoMensalEsperadoCentavos is { } r && r >= parcelaCentavos;

        var parcelaVsFolgaPercent = folgaMensalCentavos > 0
            ? Math.Round(100.0 * parcelaCentavos / folgaMensalCentavos, 1)
            : SentinelaSemFolga;

        var paybackMeses = retornoMensalEsperadoCentavos is { } retornoInformado && retornoInformado > 0
            ? (int)Math.Ceiling((double)valorCentavos / retornoInformado)
            : (int?)null;

        VeredictoViabilidade veredito;
        if (folgaEfetivaCentavos < 0)
            veredito = VeredictoViabilidade.Inviavel;
        else if (parcelaVsFolgaPercent > LimiteApertadoPercent && !retornoCobreAParcela)
            veredito = VeredictoViabilidade.Apertado;
        else
            veredito = VeredictoViabilidade.Viavel;

        var motivo = NarrarMotivo(
            veredito, parcelaCentavos, folgaMensalCentavos, parcelaVsFolgaPercent, folgaEfetivaCentavos, retornoMensalEsperadoCentavos);

        return new Viabilidade(veredito, motivo, parcelaVsFolgaPercent, paybackMeses);
    }

    private static string NarrarMotivo(
        VeredictoViabilidade veredito, long parcelaCentavos, long folgaMensalCentavos,
        double parcelaVsFolgaPercent, long folgaEfetivaCentavos, long? retornoMensalEsperadoCentavos)
    {
        var parcelaTxt = new Money(parcelaCentavos).Formatado();
        var folgaTxt = new Money(folgaMensalCentavos).Formatado();
        var percentTxt = parcelaVsFolgaPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        var baseFrase = $"A parcela de {parcelaTxt} equivale a {percentTxt}% da sua folga mensal atual ({folgaTxt}).";

        if (veredito == VeredictoViabilidade.Inviavel)
        {
            var faltaTxt = new Money(-folgaEfetivaCentavos).Formatado();
            var complemento = retornoMensalEsperadoCentavos is { } retorno
                ? $"Mesmo somando o retorno esperado do equipamento ({new Money(retorno).Formatado()}/mês), ainda faltariam {faltaTxt}/mês para cobrir a parcela"
                : $"Faltariam {faltaTxt}/mês para cobrir a parcela";
            return $"{baseFrase} {complemento} → inviável nesse prazo/valor; reveja prazo ou valor.";
        }

        if (veredito == VeredictoViabilidade.Apertado)
        {
            var sobraTxt = new Money(folgaEfetivaCentavos).Formatado();
            return $"{baseFrase} Consome mais da metade do que sobra todo mês — dá pra pagar, mas sobra só {sobraTxt}/mês de colchão; qualquer imprevisto aperta. Reveja prazo/valor se quiser mais folga.";
        }

        if (retornoMensalEsperadoCentavos is { } retornoInformado)
        {
            var retornoTxt = new Money(retornoInformado).Formatado();
            var sobraTxt = new Money(folgaEfetivaCentavos).Formatado();
            return $"{baseFrase} O equipamento retorna {retornoTxt}/mês → cobre a parcela e ainda sobra {sobraTxt}/mês → viável.";
        }

        return $"{baseFrase} Cabe com folga sobrando na sua operação atual → viável.";
    }
}
