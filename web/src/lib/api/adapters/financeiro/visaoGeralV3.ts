/**
 * DTO (.NET, `Money`/camelCase) → `VisaoGeralV3` view-models (`components/financial/visao-geral/
 * types.ts`). Funções puras, zero React — mesmo padrão de `adapters/financeiro/bancario.ts`.
 * Reproduz `docs/ui/mockups/visao-geral-v3.html` com dado REAL onde já existe read-model no
 * .NET; onde o read-model não expõe exatamente o que o mockup mostrava (ex.: descrição por dia na
 * projeção, largura de faixa do Simples), o adapter deriva a melhor aproximação HONESTA do dado
 * disponível — nunca inventa um número que não vem do servidor. Cada aproximação está documentada
 * na função que a faz.
 */
import type {
  BarraSemanal,
  FatiaMix,
  GaugeViewModel,
  MixViewModel,
  PontoTimeline,
  RoiMiniViewModel,
  SimplesMiniViewModel,
  TileAPagar,
  TileAReceber,
  TileAssinaturas,
  TileResultado,
  TimelineViewModel,
  Verdict,
} from '@/components/financial/visao-geral/types';
import type {
  ContasEmAbertoDto,
  DisponivelParaRetiradaDto,
  DreDto,
  ExtratoLinhaDto,
  FluxoDeCaixaDto,
  PrevisaoDeCaixaDto,
  RadarDoSimplesDto,
  ReceitaRecorrenteDto,
  RoiDoNegocioDto,
} from '@/lib/api/financeiro';


// ─────────────────────────── ① Gauge de fôlego ───────────────────────────

function verdictDeDias(dias: number): Verdict {
  if (dias < 15) return 'crit';
  if (dias < 30) return 'warn';
  return 'pos';
}

/** "Dias de fôlego" — `diasRunwayRealista` (já desconta sazonalidade/tendência) quando disponível,
 * senão `diasRunwayBruto` (cálculo simples saldo ÷ queima média). `null` de ambos (sem histórico
 * suficiente) vira 0 — zona crítica por padrão, nunca otimista sem dado. */
export function deGaugeDto(previsao: PrevisaoDeCaixaDto, disponivel: DisponivelParaRetiradaDto): GaugeViewModel {
  const dias = previsao.diasRunwayRealista ?? previsao.diasRunwayBruto ?? 0;
  return {
    diasFolego: Math.max(0, dias),
    verdict: verdictDeDias(dias),
    probabilidadeSaldoNegativoPercent: Math.round(previsao.probabilidadeSaldoNegativoEm30Dias * 100),
    emCaixaCentavos: disponivel.saldoEmCaixa.centavos,
    podeTirarCentavos: disponivel.podeTirar.centavos,
  };
}

// ─────────────────────────── ① Projeção do caixa ───────────────────────────

/** Índice do último ponto REALIZADO (não projetado) — é "hoje" na timeline. */
function hojeIndexDe(pontos: FluxoDeCaixaDto['pontos']): number {
  let idx = 0;
  for (let i = 0; i < pontos.length; i++) {
    if (!pontos[i].projetado) idx = i;
  }
  return idx;
}

export function deTimelineDto(dto: FluxoDeCaixaDto): TimelineViewModel {
  const pontos: PontoTimeline[] = dto.pontos.map((p) => ({
    data: p.data,
    saldoCentavos: p.saldoAcumulado.centavos,
    deltaDoDiaCentavos: p.entradas.centavos - p.saidas.centavos,
    projetado: p.projetado,
  }));

  let minIndex = 0;
  pontos.forEach((p, i) => {
    if (p.saldoCentavos < pontos[minIndex].saldoCentavos) minIndex = i;
  });

  return { pontos, hojeIndex: hojeIndexDe(dto.pontos), minIndex };
}

// ─────────────────────────── ② Tiles ───────────────────────────

export function deTileAReceber(dto: ContasEmAbertoDto): TileAReceber {
  const total = dto.receberEmAberto.centavos;
  const atrasado = dto.receberAtrasado.centavos;
  const emDia = Math.max(0, total - atrasado);
  return {
    totalCentavos: total,
    atrasadoCentavos: atrasado,
    percentEmDia: total > 0 ? Math.round((emDia / total) * 100) : 100,
    drill: { rota: '/financeiro/entradas-saidas', mensagem: '→ Entradas & Saídas — recebíveis em aberto, atrasados primeiro' },
  };
}

const DIAS_BUCKET = 30 / 4;

/** 4 baldes semanais (~7.5 dias cada) dentro dos próximos 30 dias — mesma contagem visual do
 * mockup (`.paybars` com 4 barras). `linhasSaida` já vem filtrado (extrato, tipo=saida, não pago,
 * janela de 30 dias) pelo hook — este adapter só distribui e acha o maior. */
export function deTileAPagar(contasEmAberto: ContasEmAbertoDto, linhasSaida: ExtratoLinhaDto[], hojeIso: string): TileAPagar {
  const buckets = [0, 0, 0, 0];
  const hoje = new Date(`${hojeIso}T00:00:00`).getTime();

  for (const linha of linhasSaida) {
    const dataLinha = new Date(`${linha.data.slice(0, 10)}T00:00:00`).getTime();
    const diasDesdeHoje = (dataLinha - hoje) / 86_400_000;
    const bucketIdx = Math.min(3, Math.max(0, Math.floor(diasDesdeHoje / DIAS_BUCKET)));
    buckets[bucketIdx] += linha.valor.centavos;
  }

  const maxBucket = Math.max(1, ...buckets);
  const barras: BarraSemanal[] = buckets.map((valorCentavos, i) => ({
    label: `balde ${i + 1}`,
    valorCentavos,
    alturaRelativa: valorCentavos / maxBucket,
  }));

  const maior = [...linhasSaida].sort((a, b) => b.valor.centavos - a.valor.centavos)[0];
  const maiorLabel = maior
    ? `maior: ${maior.descricao.toLowerCase()} · ${maior.data.slice(8, 10)}/${maior.data.slice(5, 7)}`
    : 'sem vencimento grande nos próximos 30 dias';

  return {
    totalCentavos: contasEmAberto.pagarEmAberto.centavos,
    barras,
    maiorLabel,
    drill: { rota: '/financeiro/entradas-saidas', mensagem: '→ Entradas & Saídas — contas a pagar dos próximos 30 dias' },
  };
}

/** "Resultado" — competência do mês corrente vs anterior (mesma fórmula de delta% da v1). Série
 * do sparkline: receita reconhecida por dia (`fato-receita-diaria`) do mês corrente — proxy
 * honesto de tendência (o DRE em si não é uma série diária), documentado no tipo. */
export function deTileResultado(dreAtual: DreDto, resultadoAnteriorCentavos: number, serieReceitaCentavos: number[]): TileResultado {
  const resultadoCentavos = dreAtual.resultadoOperacional.centavos;
  const deltaAbs = resultadoCentavos - resultadoAnteriorCentavos;
  const deltaPercentual =
    resultadoAnteriorCentavos !== 0 ? Math.round((deltaAbs / Math.abs(resultadoAnteriorCentavos)) * 100) : 0;
  const receitaBruta = dreAtual.receitaBruta.centavos;
  const margemPercent = receitaBruta > 0 ? Math.round((resultadoCentavos / receitaBruta) * 100) : 0;

  return {
    resultadoCentavos,
    deltaPercentual: Math.abs(deltaPercentual),
    deltaDirecao: deltaPercentual >= 0 ? 'up' : 'down',
    margemPercent,
    serie: serieReceitaCentavos.length >= 2 ? serieReceitaCentavos : [0, 0],
    drill: { rota: '/financeiro/relatorios', mensagem: '→ Relatórios — DRE do mês, aberto por corrente' },
  };
}

/** "Assinaturas" — MRR + ativas direto do DTO. Sparkline de 2 pontos: MRR antes do net deste mês
 * (`mrr − novo + churn`) → MRR agora — sempre derivado do próprio `ReceitaRecorrenteDto`, nunca
 * uma série fabricada. */
export function deTileAssinaturas(dto: ReceitaRecorrenteDto): TileAssinaturas {
  const mrr = dto.mrr.centavos;
  const antes = mrr - dto.mrrNovoNoMes.centavos + dto.mrrChurnNoMes.centavos;
  return {
    mrrCentavos: mrr,
    assinaturasAtivas: dto.assinaturasAtivas,
    serie: [antes, mrr],
    drill: { rota: '/financeiro/recorrentes', mensagem: '→ Recorrentes — MRR por serviço e assinaturas ativas' },
  };
}

// ─────────────────────────── ③ Mix + ROI + Simples ───────────────────────────

const CORRENTE_LABEL: Record<number, { categoria: FatiaMix['categoria']; label: string }> = {
  0: { categoria: 'recorrente', label: 'Assinaturas' },
  1: { categoria: 'servico', label: 'Serviços' },
  2: { categoria: 'comercio', label: 'Loja' },
};

/** Mix de receita — ordem visual FIXA (Serviços, Assinaturas, Loja, mesma do mockup/tokens
 * `cat-serv`/`cat-rec`/`cat-com`), não a ordem que `porCorrente` chega do servidor. */
export function deMixDto(dre: DreDto): MixViewModel {
  const totalCentavos = dre.porCorrente.reduce((acc, c) => acc + c.receitaBruta.centavos, 0) || dre.receitaBruta.centavos;
  const porOrdinal = new Map(dre.porCorrente.map((c) => [c.corrente, c]));
  const ordemVisual = [1, 0, 2] as const; // Servico, Recorrente, Comercio

  const fatias: FatiaMix[] = ordemVisual.map((ordinal) => {
    const meta = CORRENTE_LABEL[ordinal];
    const valor = porOrdinal.get(ordinal)?.receitaBruta.centavos ?? 0;
    return {
      categoria: meta.categoria,
      label: meta.label,
      percent: totalCentavos > 0 ? Math.round((valor / totalCentavos) * 100) : 0,
      drill: { rota: '/financeiro/relatorios', mensagem: '→ Relatórios — DRE por corrente (Serviços, Assinaturas, Loja)' },
    };
  });

  return { totalCentavos, fatias };
}

export function deRoiMiniDto(dto: RoiDoNegocioDto): RoiMiniViewModel {
  return {
    percentRecuperado: Math.round(dto.recuperacao.percentRecuperado * 100),
    recuperadoCentavos: dto.recuperacao.recuperadoCentavos,
    totalCentavos: dto.investimento.totalCentavos,
    drill: { rota: '/financeiro/roi-negocio', mensagem: '→ Investimento & ROI — curva investido × recuperado' },
  };
}

/**
 * "Simples Nacional" — `proximidadeDoProximoDegrauPercent` é uma APROXIMAÇÃO: o DTO
 * (`RadarDoSimplesDto`) só expõe a distância em R$ até o próximo degrau, não a largura da faixa
 * atual (que exigiria a tabela de anexos completa no cliente). Usa-se `1 − distância/RBT12` como
 * proxy de "quanto do RBT12 atual já empurra contra o teto" — quanto maior o RBT12 relativo à
 * distância que falta, mais perto do degrau. Documentado aqui para não ser confundido com um
 * cálculo exato vindo do servidor.
 */
export function deSimplesMiniDto(dto: RadarDoSimplesDto): SimplesMiniViewModel {
  const proximidade = dto.rbt12Centavos > 0 ? 1 - Math.min(1, dto.distanciaAoProximoDegrauCentavos / dto.rbt12Centavos) : 0;
  return {
    aliquotaEfetivaPercent: Math.round(dto.aliquotaEfetiva * 1000) / 10,
    faixaAtual: dto.faixaAtual,
    proximidadeDoProximoDegrauPercent: Math.round(Math.max(0, Math.min(1, proximidade)) * 100),
    mesesAteProximoDegrau: dto.mesesProjetadosAteOProximoDegrau,
    drill: { rota: '/financeiro/relatorios', mensagem: '→ Relatórios — Radar do Simples (RBT12, faixa e degraus)' },
  };
}
