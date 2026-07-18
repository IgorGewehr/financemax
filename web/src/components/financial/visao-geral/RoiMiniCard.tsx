import { formatCentavosWhole } from '@/lib/money';

import type { DrillTarget, RoiMiniViewModel } from './types';

interface RoiMiniCardProps {
  vm: RoiMiniViewModel;
  onDrill: (target: DrillTarget) => void;
}

/** ③b "Investimento" — só renderiza com `imobilizadoRoiAtivo` ligado (Financeiro ›
 * Configurações), nunca um botão flutuante de opt-in na própria tela (esse era só um afordance
 * de DEMO do mockup — a config real mora em `/financeiro/configuracoes`). */
export function RoiMiniCard({ vm, onDrill }: RoiMiniCardProps) {
  return (
    <button
      type="button"
      onClick={() => onDrill(vm.drill)}
      className="group relative flex flex-col justify-center gap-2 rounded-2xl border border-border bg-card p-4 pb-3.5 text-left shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary-600/45 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="pointer-events-none absolute right-3.5 top-3 text-[13px] text-primary-600 opacity-0 transition-opacity group-hover:opacity-100">→</span>
      <span className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">Investimento</span>
      <div className="text-[25px] font-extrabold leading-tight tracking-tight text-foreground">
        <span className="num">{vm.percentRecuperado}%</span>
        <small className="ml-1.5 text-[12.5px] font-semibold text-muted-foreground">recuperado</small>
      </div>
      <div className="relative mt-0.5 h-2 overflow-hidden rounded-full bg-pos-soft">
        <span className="absolute inset-y-0 left-0 rounded-full bg-pos" style={{ width: `${Math.min(100, vm.percentRecuperado)}%` }} />
      </div>
      <span className="num text-[11.5px] text-muted-foreground">
        {formatCentavosWhole(vm.recuperadoCentavos)} de {formatCentavosWhole(vm.totalCentavos)}
      </span>
    </button>
  );
}
