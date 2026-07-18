import { formatCentavosWhole } from '@/lib/money';

import type { DrillTarget, MixViewModel } from './types';

interface MixCardProps {
  vm: MixViewModel;
  onDrill: (target: DrillTarget) => void;
}

const CX = 58;
const CY = 58;
const R = 44;
const SW = 15;
const GAPF = 4 / 360;

const CATEGORIA_VAR: Record<MixViewModel['fatias'][number]['categoria'], string> = {
  servico: '--cat-serv',
  recorrente: '--cat-rec',
  comercio: '--cat-com',
};

function pontoNoAnel(fracao: number): [number, number] {
  const angulo = -Math.PI / 2 + 2 * Math.PI * fracao;
  return [CX + R * Math.cos(angulo), CY + R * Math.sin(angulo)];
}

/** ③a "De onde vem" — rosca com as 3 correntes de receita (Serviços/Assinaturas/Loja), mesma
 * geometria SVG do mockup v3. */
export function MixCard({ vm, onDrill }: MixCardProps) {
  let acumulado = 0;
  const arcos = vm.fatias.map((fatia) => {
    const fracao = fatia.percent / 100;
    const f0 = acumulado + GAPF / 2;
    const f1 = acumulado + fracao - GAPF / 2;
    acumulado += fracao;
    const [x0, y0] = pontoNoAnel(f0);
    const [x1, y1] = pontoNoAnel(f1);
    return { fatia, d: `M ${x0.toFixed(1)},${y0.toFixed(1)} A ${R},${R} 0 ${f1 - f0 > 0.5 ? 1 : 0} 1 ${x1.toFixed(1)},${y1.toFixed(1)}` };
  });

  return (
    <button
      type="button"
      onClick={() => onDrill(vm.fatias[0]?.drill ?? { rota: '/financeiro/relatorios', mensagem: '→ Relatórios' })}
      className="group relative flex w-full flex-col gap-1.5 rounded-2xl border border-border bg-card p-4 pb-3.5 text-left shadow-sm transition-all hover:-translate-y-0.5 hover:border-primary-600/45 hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <span className="pointer-events-none absolute right-3.5 top-3 text-[13px] text-primary-600 opacity-0 transition-opacity group-hover:opacity-100">→</span>
      <span className="text-[11px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">De onde vem</span>

      <div className="flex items-center gap-4">
        <div
          className="relative h-[116px] w-[116px] shrink-0"
          role="img"
          aria-label={`Receita do mês, ${formatCentavosWhole(vm.totalCentavos)}: ${vm.fatias.map((f) => `${f.label} ${f.percent}%`).join(', ')}`}
        >
          <svg viewBox="0 0 116 116" aria-hidden="true" className="block h-[116px] w-[116px]">
            {arcos.map(({ fatia, d }) => (
              <path key={fatia.categoria} d={d} fill="none" stroke={`hsl(var(${CATEGORIA_VAR[fatia.categoria]}))`} strokeWidth={SW} />
            ))}
          </svg>
          <div className="pointer-events-none absolute inset-0 grid place-items-center text-center">
            <div>
              <div className="num text-[14.5px] font-extrabold tracking-tight text-foreground">{formatCentavosWhole(vm.totalCentavos)}</div>
              <div className="text-[10.5px] font-semibold text-muted-foreground">no mês</div>
            </div>
          </div>
        </div>

        <div className="flex min-w-0 flex-1 flex-col gap-2">
          {vm.fatias.map((fatia) => (
            <span key={fatia.categoria} className="flex items-center gap-2.5 text-[12.5px] font-semibold">
              <span className="h-[9px] w-[9px] shrink-0 rounded-[3px]" style={{ background: `hsl(var(${CATEGORIA_VAR[fatia.categoria]}))` }} />
              <span className="min-w-0 flex-1 truncate">{fatia.label}</span>
              <span className="num text-[13px] font-bold">{fatia.percent}%</span>
            </span>
          ))}
        </div>
      </div>
    </button>
  );
}
