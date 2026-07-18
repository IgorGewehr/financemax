import { formatPercent } from '@/lib/format';

import type { DrillTarget, SimplesMiniViewModel } from './types';

interface SimplesMiniCardProps {
  vm: SimplesMiniViewModel;
  onDrill: (target: DrillTarget) => void;
}

function labelDegrau(meses: number | null): string {
  if (meses === null) return 'sem projeção de degrau';
  if (meses <= 0) return 'no limite da faixa';
  if (meses <= 2) return `degrau perto (~${meses} ${meses === 1 ? 'mês' : 'meses'})`;
  return `degrau longe (~${meses} meses)`;
}

/** ③c "Simples Nacional" — sempre visível (não é opt-in, diferente do ROI). */
export function SimplesMiniCard({ vm, onDrill }: SimplesMiniCardProps) {
  return (
    <button
      type="button"
      onClick={() => onDrill(vm.drill)}
      className="group relative flex flex-col justify-center gap-2 rounded-2xl border border-border bg-card p-4 pb-3.5 text-left shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary-600/45 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="pointer-events-none absolute right-3.5 top-3 text-[13px] text-primary-600 opacity-0 transition-opacity group-hover:opacity-100">→</span>
      <span className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Simples Nacional</span>
      <div className="text-[25px] font-extrabold leading-tight tracking-tight text-foreground">
        <span className="num">{formatPercent(vm.aliquotaEfetivaPercent, 1)}</span>
        <small className="ml-1.5 text-[12.5px] font-semibold text-muted-foreground">de imposto</small>
      </div>
      <div className="relative mt-0.5 h-2 overflow-hidden rounded-full bg-surface-2">
        <span className="absolute inset-y-0 left-0 rounded-full bg-cat-serv" style={{ width: `${vm.proximidadeDoProximoDegrauPercent}%` }} />
      </div>
      <span className="text-[11.5px] text-muted-foreground">
        faixa {vm.faixaAtual} · {labelDegrau(vm.mesesAteProximoDegrau)}
      </span>
    </button>
  );
}
