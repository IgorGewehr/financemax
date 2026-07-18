/**
 * View-models da Visão Geral v3 — espelha 1:1 `docs/ui/mockups/visao-geral-v3.html` (SistemaX,
 * copiado para `financemax/docs/ui/mockups/visao-geral-v3.html`). Substitui a v1/v2 (hero +
 * decomposição + Super Consultor) inteira: v3 é gauge de fôlego + projeção de caixa + 4 tiles +
 * mix de receita + ROI/Simples, SEM Super Consultor e SEM título "Visão Geral" (a aba já nomeia).
 *
 * Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 */
import type { Centavos } from '@/lib/money';

/** Rotas reais do módulo Financeiro (mesmas do `FinanceiroLayout`) — destino dos drills desta tela. */
export type FinanceiroRoute =
  | '/financeiro/entradas-saidas'
  | '/financeiro/recorrentes'
  | '/financeiro/bancario'
  | '/financeiro/fluxo-de-caixa'
  | '/financeiro/roi-negocio'
  | '/financeiro/relatorios';

/** Alvo de um "drill" (clique que leva a outra aba do Financeiro — Lei 2 permite link de
 * navegação, nunca ação). `mensagem` é o texto de contexto mostrado no toast, mesmo padrão do
 * `data-msg` do mockup. */
export interface DrillTarget {
  rota: FinanceiroRoute;
  mensagem: string;
}

export type Verdict = 'pos' | 'warn' | 'crit';

/** ① Gauge "Saúde do negócio" — dias de fôlego (`previsao-caixa`), zonas fixas: crit < 15,
 * warn 15–30, pos ≥ 30 (mesmo corte do mockup). */
export interface GaugeViewModel {
  diasFolego: number;
  verdict: Verdict;
  probabilidadeSaldoNegativoPercent: number;
  emCaixaCentavos: Centavos;
  podeTirarCentavos: Centavos;
}

export interface PontoTimeline {
  data: string;
  saldoCentavos: Centavos;
  /** Diferença líquida do dia (entradas − saídas) — usada só no tooltip ao clicar num ponto. */
  deltaDoDiaCentavos: Centavos;
  projetado: boolean;
}

/** ① Projeção do caixa — `GET /financeiro/fluxo` (14 dias realizado + 30 projetado). */
export interface TimelineViewModel {
  pontos: PontoTimeline[];
  hojeIndex: number;
  minIndex: number;
}

/** ② Um dos 4 tiles escaneáveis (a receber / a pagar / resultado / assinaturas). */
export interface TileBase {
  drill: DrillTarget;
}

export interface TileAReceber extends TileBase {
  totalCentavos: Centavos;
  atrasadoCentavos: Centavos;
  /** 0–100, fração em dia (o resto é atrasado). */
  percentEmDia: number;
}

export interface BarraSemanal {
  label: string;
  valorCentavos: Centavos;
  /** 0–1 relativo ao maior valor das barras — altura visual. */
  alturaRelativa: number;
}

export interface TileAPagar extends TileBase {
  totalCentavos: Centavos;
  barras: BarraSemanal[];
  maiorLabel: string;
}

export interface TileResultado extends TileBase {
  resultadoCentavos: Centavos;
  deltaPercentual: number;
  deltaDirecao: 'up' | 'down';
  margemPercent: number;
  /** Série curta (≥ 2 pontos) pro sparkline — tendência de receita reconhecida no mês corrente
   * (`fato-receita-diaria`), proxy honesto: o DRE em si não tem série diária. */
  serie: number[];
}

export interface TileAssinaturas extends TileBase {
  mrrCentavos: Centavos;
  assinaturasAtivas: number;
  /** 2 pontos derivados de `mrrNovoNoMes`/`mrrChurnNoMes` (antes do net deste mês → depois) —
   * nunca um dado fabricado, sempre a diferença real do próprio DTO. */
  serie: number[];
}

export interface TilesViewModel {
  aReceber: TileAReceber;
  aPagar: TileAPagar;
  resultado: TileResultado;
  assinaturas: TileAssinaturas;
}

/** ③a Mix de receita — as 3 correntes (`CorrenteDeReceita`), sempre nesta ordem visual (Serviços,
 * Assinaturas, Loja — mesma do mockup/tokens `cat-serv`/`cat-rec`/`cat-com`). */
export interface FatiaMix {
  categoria: 'servico' | 'recorrente' | 'comercio';
  label: string;
  percent: number;
  drill: DrillTarget;
}

export interface MixViewModel {
  totalCentavos: Centavos;
  fatias: FatiaMix[];
}

/** ③b Investimento — opt-in (`imobilizadoRoiAtivo`), 404 se desligado. */
export interface RoiMiniViewModel {
  percentRecuperado: number;
  recuperadoCentavos: Centavos;
  totalCentavos: Centavos;
  drill: DrillTarget;
}

/** ③c Simples Nacional — sempre visível (não é opt-in). */
export interface SimplesMiniViewModel {
  aliquotaEfetivaPercent: number;
  faixaAtual: number;
  /** 0–100 — aproximação de "quanto da distância até o próximo degrau já foi consumida pelo
   * RBT12 atual" (o DTO não expõe a largura da faixa, só a distância em R$ — ver adapter). */
  proximidadeDoProximoDegrauPercent: number;
  mesesAteProximoDegrau: number | null;
  drill: DrillTarget;
}
