import type { Centavos } from '@/lib/money';

import type { ContaBancaria, MovimentoExtrato, SemanaMovimento } from './types';

/** Helpers puros e testáveis — nenhum componente calcula estes números inline. */

export const somaCentavos = (valores: Centavos[]): Centavos => valores.reduce((a, b) => a + b, 0);

export function saldoTotalContas(contas: ContaBancaria[]): Centavos {
  return somaCentavos(contas.map((c) => c.saldoCentavos));
}

export function semanaEntrouTotal(semana: SemanaMovimento): Centavos {
  return somaCentavos(semana.entrouPorDiaCentavos);
}

export function semanaSaiuTotal(semana: SemanaMovimento): Centavos {
  return somaCentavos(semana.saiuPorDiaCentavos);
}

export function mesEntrouTotal(semanas: SemanaMovimento[]): Centavos {
  return somaCentavos(semanas.map(semanaEntrouTotal));
}

export function mesSaiuTotal(semanas: SemanaMovimento[]): Centavos {
  return somaCentavos(semanas.map(semanaSaiuTotal));
}

/** Rodapé do KPI "Saldo em bancos" — "Itaú R$ 8.120 · Nubank R$ 3.410 · Stone R$ 1.210". */
export function formatSaldoFoot(contas: ContaBancaria[], formatCentavos: (v: Centavos) => string): string {
  return contas.map((c) => `${c.label} ${formatCentavos(c.saldoCentavos)}`).join(' · ');
}

/**
 * Ordena o extrato do mais recente pro mais antigo. Replica a chave do mockup
 * (`semana*100 + dia-do-mês`) — não é uma data real, só preserva a ordem visual esperada.
 */
export function ordenarMovimentosDesc(movimentos: MovimentoExtrato[]): MovimentoExtrato[] {
  const chave = (m: MovimentoExtrato) => m.semanaId * 100 + parseInt(m.data, 10);
  return [...movimentos].sort((a, b) => chave(b) - chave(a));
}

export function pendingCount(banco: unknown[], sistema: unknown[]): number {
  return banco.length + sistema.length;
}

export function pendingTotalCentavos(
  banco: { valorCentavos: Centavos }[],
  sistema: { valorCentavos: Centavos }[],
): Centavos {
  return (
    somaCentavos(banco.map((i) => Math.abs(i.valorCentavos))) +
    somaCentavos(sistema.map((i) => Math.abs(i.valorCentavos)))
  );
}

/** Item de entrada do gráfico divergente (entrou acima do zero / saiu abaixo). */
export interface DivergentChartItem {
  id: string;
  label: string;
  entrouCentavos: Centavos;
  saiuCentavos: Centavos;
  muted?: boolean;
}

export interface DivergentBarGeom {
  id: string;
  label: string;
  entrouCentavos: Centavos;
  saiuCentavos: Centavos;
  barX: number;
  barWidth: number;
  upY: number;
  upHeight: number;
  downY: number;
  downHeight: number;
  opacity: number;
  colBg?: { x: number; y: number; width: number; height: number };
  labelX: number;
  labelY: number;
}

export interface DivergentChartLayout {
  viewBox: string;
  zeroY: number;
  x0: number;
  x1: number;
  bars: DivergentBarGeom[];
}

export interface SaldoSparklineGeom {
  /** "M x,y L x,y ..." — traçado central da linha, pronto pro atributo `d`. */
  linePath: string;
  /** Mesmo traçado fechado na base (y=34) — pro preenchimento em gradiente sob a linha. */
  areaPath: string;
  /** Ponto mais recente (saldo de hoje) — onde a bolinha de destaque é desenhada. */
  lastPoint: { x: number; y: number };
}

const SPARK_WIDTH = 260;
const SPARK_TOP = 4;
const SPARK_BOTTOM = 28;
const SPARK_BASELINE = 34;

/**
 * Sparkline REAL do KPI "Saldo em bancos" — reconstrói o saldo de fim de cada semana a partir do
 * saldo atual das contas e do líquido (entrou − saiu) de cada semana, "desfazendo" semana a semana
 * pra trás a partir de hoje (não existe snapshot histórico de saldo por dia, só o saldo atual + os
 * movimentos do período). Sem nenhuma semana carregada (ou sem variação no período), cai numa linha
 * reta neutra na altura do saldo atual — nunca uma curva inventada.
 */
export function computeSaldoSparkline(contas: ContaBancaria[], semanas: SemanaMovimento[]): SaldoSparklineGeom {
  const saldoAtual = saldoTotalContas(contas);
  const ordenadas = [...semanas].sort((a, b) => a.id - b.id);

  const balances: number[] = [saldoAtual];
  for (let i = ordenadas.length - 1; i >= 0; i--) {
    const net = semanaEntrouTotal(ordenadas[i]) - semanaSaiuTotal(ordenadas[i]);
    balances.unshift(balances[0] - net);
  }

  const n = balances.length;
  const minB = Math.min(...balances);
  const maxB = Math.max(...balances);
  const range = maxB - minB;
  const midY = (SPARK_TOP + SPARK_BOTTOM) / 2;

  const points =
    n <= 1
      ? [
          { x: 0, y: midY },
          { x: SPARK_WIDTH, y: midY },
        ]
      : balances.map((b, i) => ({
          x: (i * SPARK_WIDTH) / (n - 1),
          y: range === 0 ? midY : SPARK_BOTTOM - ((b - minB) / range) * (SPARK_BOTTOM - SPARK_TOP),
        }));

  const linePath = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(2)},${p.y.toFixed(2)}`).join(' ');
  const first = points[0];
  const last = points[points.length - 1];
  const baseline = SPARK_BASELINE.toFixed(2);
  const areaPath = `${linePath} L${last.x.toFixed(2)},${baseline} L${first.x.toFixed(2)},${baseline} Z`;

  return { linePath, areaPath, lastPoint: last };
}

/**
 * Geometria do gráfico "entrou × saiu" (barras divergentes a partir de uma linha zero).
 * Réplica matemática 1:1 da função `svgDivergent` do mockup — mesma escala/proporção.
 */
export function computeDivergentLayout(items: DivergentChartItem[], clickable: boolean): DivergentChartLayout {
  const zeroY = 62;
  const x0 = 18;
  const x1 = 332;
  const top = 8;
  const bottom = 150;
  const maxV = Math.max(1, ...items.map((i) => i.entrouCentavos), ...items.map((i) => i.saiuCentavos));
  const capUp = zeroY - top;
  const capDown = bottom - zeroY - 20;
  const f = Math.min(capUp, capDown) / maxV;
  const slot = items.length > 0 ? (x1 - x0) / items.length : x1 - x0;

  const bars: DivergentBarGeom[] = items.map((it, i) => {
    const cx = x0 + slot * i + slot / 2;
    const bw = Math.min(30, slot * 0.5);
    const hUp = it.entrouCentavos * f;
    const hDown = it.saiuCentavos * f;
    return {
      id: it.id,
      label: it.label,
      entrouCentavos: it.entrouCentavos,
      saiuCentavos: it.saiuCentavos,
      barX: cx - bw / 2,
      barWidth: bw,
      upY: zeroY - hUp,
      upHeight: hUp,
      downY: zeroY,
      downHeight: hDown,
      opacity: it.muted ? 0.55 : 1,
      colBg: clickable ? { x: x0 + slot * i + 1, y: top, width: slot - 2, height: bottom - top - 14 } : undefined,
      labelX: cx,
      labelY: 146,
    };
  });

  return { viewBox: '0 0 350 150', zeroY, x0, x1, bars };
}
