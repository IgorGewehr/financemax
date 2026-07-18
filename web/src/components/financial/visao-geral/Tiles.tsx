import type { ReactNode } from 'react';

import { formatPercent } from '@/lib/format';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import type { DrillTarget, TilesViewModel } from './types';

interface TilesProps {
  vm: TilesViewModel;
  onDrill: (target: DrillTarget) => void;
}

function TileShell({ drill, onDrill, children }: { drill: DrillTarget; onDrill: (t: DrillTarget) => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={() => onDrill(drill)}
      className="group relative flex flex-col gap-2.5 rounded-2xl border border-border bg-card p-4 pb-3.5 text-left shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary-600/45 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="pointer-events-none absolute right-3.5 top-3 text-[13px] text-primary-600 opacity-0 transition-opacity group-hover:opacity-100">→</span>
      {children}
    </button>
  );
}

function Eyebrow({ children }: { children: ReactNode }) {
  return <span className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">{children}</span>;
}

/** Sparkline minúsculo compartilhado dos tiles "Resultado"/"Assinaturas" — mesma geometria do
 * mockup (`viewBox 0 0 122 30`, ponto final em destaque). Aceita 2+ pontos. */
function MiniTrendSpark({ serie }: { serie: number[] }) {
  const min = Math.min(...serie);
  const max = Math.max(...serie, min + 1);
  const n = serie.length;
  const xFor = (i: number) => 2 + i * (115 / Math.max(1, n - 1));
  const yFor = (v: number) => 27 - ((v - min) / (max - min || 1)) * 22;
  const points = serie.map((v, i) => `${xFor(i).toFixed(1)},${yFor(v).toFixed(1)}`).join(' ');
  const ultimo = { x: xFor(n - 1), y: yFor(serie[n - 1]) };

  return (
    <svg viewBox="0 0 122 30" className="block h-[30px] w-full max-w-[128px]" aria-hidden="true">
      <polyline points={points} fill="none" className="stroke-faint" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
      <circle cx={ultimo.x} cy={ultimo.y} r={3.5} className="fill-pos stroke-card" strokeWidth={2} />
    </svg>
  );
}

/** ② Tiles escaneáveis: a receber, a pagar, resultado, assinaturas. */
export function Tiles({ vm, onDrill }: TilesProps) {
  return (
    <div className="mb-4 grid grid-cols-1 gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
      <TileShell drill={vm.aReceber.drill} onDrill={onDrill}>
        <Eyebrow>A receber</Eyebrow>
        <MoneyValue centavos={vm.aReceber.totalCentavos} className="text-[25px] font-extrabold leading-tight tracking-tight" />
        <div className="flex h-[7px] w-full max-w-[128px] gap-0.5 overflow-hidden rounded-full">
          <span className="block h-full bg-pos" style={{ width: `${vm.aReceber.percentEmDia}%` }} />
          <span className="block h-full bg-warn" style={{ width: `${100 - vm.aReceber.percentEmDia}%` }} />
        </div>
        <span className="text-[11.5px] text-muted-foreground">
          <span className="num font-bold text-warn">{formatCentavosWhole(vm.aReceber.atrasadoCentavos)}</span> atrasado
        </span>
      </TileShell>

      <TileShell drill={vm.aPagar.drill} onDrill={onDrill}>
        <Eyebrow>A pagar</Eyebrow>
        <MoneyValue centavos={vm.aPagar.totalCentavos} className="text-[25px] font-extrabold leading-tight tracking-tight" />
        <div className="flex h-[28px] items-end gap-1.5">
          {vm.aPagar.barras.map((b, i) => (
            <span
              key={i}
              title={`${b.label}: ${formatCentavosWhole(b.valorCentavos)}`}
              className={cn('block w-5 rounded-t-[3px]', b.alturaRelativa >= 0.85 ? 'bg-foreground/55' : 'bg-foreground/22')}
              style={{ height: `${Math.max(3, b.alturaRelativa * 28)}px` }}
            />
          ))}
        </div>
        <span className="text-[11.5px] text-muted-foreground">{vm.aPagar.maiorLabel}</span>
      </TileShell>

      <TileShell drill={vm.resultado.drill} onDrill={onDrill}>
        <Eyebrow>Resultado</Eyebrow>
        <div className="flex items-baseline gap-2">
          <MoneyValue centavos={vm.resultado.resultadoCentavos} className="text-[25px] font-extrabold leading-tight tracking-tight" />
          <span className={cn('inline-flex items-center gap-0.5 text-xs font-bold', vm.resultado.deltaDirecao === 'up' ? 'text-pos' : 'text-crit')}>
            {vm.resultado.deltaDirecao === 'up' ? '▲' : '▼'} {formatPercent(vm.resultado.deltaPercentual)}
          </span>
        </div>
        <MiniTrendSpark serie={vm.resultado.serie} />
        <span className="text-[11.5px] text-muted-foreground">margem {formatPercent(vm.resultado.margemPercent)}</span>
      </TileShell>

      <TileShell drill={vm.assinaturas.drill} onDrill={onDrill}>
        <Eyebrow>Assinaturas</Eyebrow>
        <div className="flex items-baseline gap-1.5">
          <MoneyValue centavos={vm.assinaturas.mrrCentavos} className="text-[25px] font-extrabold leading-tight tracking-tight" />
          <small className="text-[13px] font-semibold text-muted-foreground">/mês</small>
        </div>
        <MiniTrendSpark serie={vm.assinaturas.serie} />
        <span className="text-[11.5px] text-muted-foreground">{vm.assinaturas.assinaturasAtivas} ativas</span>
      </TileShell>
    </div>
  );
}
