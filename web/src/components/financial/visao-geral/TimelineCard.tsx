import { useMemo, useState } from 'react';

import { Surface } from '@/components/ui/Surface';
import { formatDateShort } from '@/lib/format';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

import type { TimelineViewModel } from './types';

interface TimelineCardProps {
  vm: TimelineViewModel;
}

const W = 880;
const H = 226;
const PAD_L = 14;
const PAD_R = 14;
const PAD_TOP = 30;
const PAD_BOTTOM = 28;

/** ① Projeção do caixa — 14 dias realizados + hoje + 30 previstos, curva sólida→tracejada,
 * lavagem de área e o ponto mais apertado em destaque (mesma leitura visual do mockup v3). Clique
 * num dia mostra saldo + variação líquida daquele dia (sem descrição — `PontoFluxoCaixa` do .NET
 * não carrega origem por dia, ver `adapters/financeiro/visaoGeralV3.ts`). */
export function TimelineCard({ vm }: TimelineCardProps) {
  const [selecionado, setSelecionado] = useState<number | null>(null);

  const geometria = useMemo(() => {
    const { pontos } = vm;
    const n = pontos.length;
    if (n === 0) return null;

    const valores = pontos.map((p) => p.saldoCentavos);
    const min = Math.min(0, ...valores);
    const max = Math.max(1, ...valores) * 1.08;
    const plotW = W - PAD_L - PAD_R;
    const plotH = H - PAD_TOP - PAD_BOTTOM;

    const xFor = (i: number) => PAD_L + i * (plotW / Math.max(1, n - 1));
    const yFor = (v: number) => PAD_TOP + ((max - v) / (max - min || 1)) * plotH;
    const y0 = yFor(0);

    const pathFor = (idxs: number[]) => idxs.map((i, k) => `${k === 0 ? 'M' : 'L'}${xFor(i).toFixed(1)},${yFor(valores[i]).toFixed(1)}`).join(' ');

    const idxAll = pontos.map((_, i) => i);
    const solidPath = pathFor(idxAll.slice(0, vm.hojeIndex + 1));
    const dashPath = pathFor(idxAll.slice(vm.hojeIndex));
    const areaPath = `${pathFor(idxAll)} L ${xFor(n - 1).toFixed(1)},${y0.toFixed(1)} L ${xFor(0).toFixed(1)},${y0.toFixed(1)} Z`;

    return { n, xFor, yFor, y0, solidPath, dashPath, areaPath, slot: plotW / n };
  }, [vm]);

  if (!geometria) return null;
  const { n, xFor, yFor, y0, solidPath, dashPath, areaPath, slot } = geometria;
  const minPonto = vm.pontos[vm.minIndex];
  const ultimoPonto = vm.pontos[n - 1];
  const selPonto = selecionado !== null ? vm.pontos[selecionado] : null;

  return (
    <Surface padding="none" className="flex h-full flex-col pb-1.5">
      <div className="flex items-center justify-between gap-2.5 px-[18px] pb-0.5 pt-4">
        <h2 className="text-[13px] font-bold text-foreground">Caixa · próximos 30 dias</h2>
        <span className="text-xs font-medium text-muted-foreground">toque num dia</span>
      </div>
      <div className="flex gap-4 px-[18px] pt-1.5 text-xs text-muted-foreground">
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block h-[2.5px] w-4 rounded bg-foreground" />
          realizado
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block h-0 w-4 border-t-[2.5px] border-dashed border-foreground/70" />
          previsto
        </span>
      </div>

      <div className="relative mx-2 mb-2 mt-0.5 flex-1">
        <svg viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`Projeção do caixa: ponto mais baixo ${formatCentavosWhole(minPonto?.saldoCentavos)}.`} className="block w-full">
          <defs>
            <linearGradient id="vg3-area" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="hsl(var(--pos))" stopOpacity={0.14} />
              <stop offset="1" stopColor="hsl(var(--pos))" stopOpacity={0} />
            </linearGradient>
          </defs>

          <path d={areaPath} fill="url(#vg3-area)" />
          <line x1={PAD_L} y1={y0} x2={W - PAD_R} y2={y0} className="stroke-border" strokeWidth={1} />

          <line x1={xFor(vm.hojeIndex)} y1={PAD_TOP - 6} x2={xFor(vm.hojeIndex)} y2={H - PAD_BOTTOM} className="stroke-muted-foreground" strokeWidth={1} strokeDasharray="2 3" opacity={0.5} />

          <path d={solidPath} fill="none" className="stroke-foreground" strokeWidth={2.3} strokeLinecap="round" strokeLinejoin="round" />
          <path d={dashPath} fill="none" className="stroke-foreground" strokeWidth={2.3} strokeDasharray="1 5.5" strokeLinecap="round" strokeLinejoin="round" opacity={0.8} />

          {minPonto && (
            <circle cx={xFor(vm.minIndex)} cy={yFor(minPonto.saldoCentavos)} r={4.5} className="fill-warn stroke-card" strokeWidth={2.2} />
          )}
          {ultimoPonto && (
            <circle cx={xFor(n - 1)} cy={yFor(ultimoPonto.saldoCentavos)} r={3.5} className="fill-foreground stroke-card" strokeWidth={2} />
          )}

          {vm.pontos.map((_, i) => (
            <rect
              key={i}
              data-i={i}
              x={xFor(i) - slot / 2}
              y={PAD_TOP - 6}
              width={slot}
              height={H - PAD_BOTTOM - (PAD_TOP - 6)}
              fill="transparent"
              className="cursor-pointer"
              onClick={() => setSelecionado((atual) => (atual === i ? null : i))}
            />
          ))}
        </svg>

        {selPonto && (
          <div
            className="pointer-events-none absolute z-[7] min-w-[172px] -translate-x-1/2 -translate-y-[122%] rounded-[10px] bg-foreground px-2.5 py-2 text-[11.5px] leading-relaxed text-background shadow-lg"
            style={{ left: `${(xFor(selecionado!) / W) * 100}%`, top: `${(yFor(selPonto.saldoCentavos) / H) * 100}%` }}
          >
            <div className="font-bold">
              {formatDateShort(selPonto.data)}
              {selecionado === vm.hojeIndex ? ' · hoje' : ''}
            </div>
            <div className="num opacity-90">
              Saldo: <b>{formatCentavosWhole(selPonto.saldoCentavos)}</b>
            </div>
            <div
              className={cn(
                'mt-1 font-semibold',
                selPonto.deltaDoDiaCentavos > 0 ? 'text-pos' : selPonto.deltaDoDiaCentavos < 0 ? 'text-crit' : 'text-background/60 font-normal',
              )}
            >
              {selPonto.deltaDoDiaCentavos === 0
                ? 'Dia comum — só o movimento do balcão.'
                : `${selPonto.deltaDoDiaCentavos > 0 ? '+' : '−'}${formatCentavosWhole(Math.abs(selPonto.deltaDoDiaCentavos))} no dia`}
            </div>
          </div>
        )}
      </div>
    </Surface>
  );
}
