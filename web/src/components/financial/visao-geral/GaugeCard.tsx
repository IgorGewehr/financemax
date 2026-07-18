import { Eyebrow, InfoTip } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { MoneyValue } from './MoneyValue';
import type { DrillTarget, GaugeViewModel } from './types';

interface GaugeCardProps {
  vm: GaugeViewModel;
  onDrill: (target: DrillTarget) => void;
}

const VERDICT_LABEL: Record<GaugeViewModel['verdict'], string> = { pos: 'Saudável', warn: 'Atenção', crit: 'Crítico' };
const VERDICT_CLASS: Record<GaugeViewModel['verdict'], string> = {
  pos: 'bg-pos-soft text-pos border border-pos/35',
  warn: 'bg-warn-soft text-warn border border-warn/35',
  crit: 'bg-crit-soft text-crit border border-crit/35',
};

const W = 236;
const H = 136;
const CX = 118;
const CY = 120;
const R = 94;
const SW = 15;
const MAX = 60;
const GAPF = 2.4 / 180;

function pontoNoArco(fracao: number, raio: number): [number, number] {
  const angulo = Math.PI * (1 - fracao);
  return [CX + raio * Math.cos(angulo), CY - raio * Math.sin(angulo)];
}

function arco(f0: number, f1: number, raio: number): string {
  const [x0, y0] = pontoNoArco(f0, raio);
  const [x1, y1] = pontoNoArco(f1, raio);
  return `M ${x0.toFixed(1)},${y0.toFixed(1)} A ${raio},${raio} 0 ${f1 - f0 > 0.5 ? 1 : 0} 1 ${x1.toFixed(1)},${y1.toFixed(1)}`;
}

const ZONAS: Array<[number, number, GaugeViewModel['verdict']]> = [
  [0, 15, 'crit'],
  [15, 30, 'warn'],
  [30, MAX, 'pos'],
];

const TONE_VAR: Record<GaugeViewModel['verdict'], string> = { pos: '--pos', warn: '--warn', crit: '--crit' };

/** Arco de 3 zonas (crit/warn/pos) + marcador no valor atual — mesma geometria SVG do mockup
 * (`docs/ui/mockups/visao-geral-v3.html`), portada de JS puro para JSX. */
function GaugeSvg({ diasFolego }: { diasFolego: number }) {
  const valor = Math.min(diasFolego, MAX);
  const [mx, my] = pontoNoArco(valor / MAX, R);

  return (
    <svg viewBox={`0 0 ${W} ${H}`} aria-hidden="true" className="block w-full">
      {ZONAS.map(([a, b, tone]) => {
        const f0 = a / MAX + (a > 0 ? GAPF : 0);
        const f1 = b / MAX - (b < MAX ? GAPF : 0);
        const ativa = diasFolego >= a && (diasFolego < b || b === MAX);
        return (
          <path
            key={tone}
            d={arco(f0, f1, R)}
            fill="none"
            stroke={`hsl(var(${TONE_VAR[tone]}))`}
            strokeWidth={SW}
            strokeLinecap="round"
            opacity={ativa ? 1 : 0.22}
          />
        );
      })}
      <circle cx={mx} cy={my} r={7} className="fill-foreground stroke-card" strokeWidth={3} />
      <text x={CX - R} y={CY + 16} fontSize={9.5} textAnchor="middle" className="fill-muted-foreground">0</text>
      <text x={CX + R} y={CY + 16} fontSize={9.5} textAnchor="middle" className="fill-muted-foreground">60+</text>
    </svg>
  );
}

/** ① Gauge "Saúde do negócio" — dias de fôlego (`previsao-caixa`) + chips "Em caixa"/"Pode
 * tirar" (`disponivel-retirada`). Bloco dominante da Visão Geral v3. */
export function GaugeCard({ vm, onDrill }: GaugeCardProps) {
  return (
    <Surface padding="none" className="flex h-full flex-col p-5 pb-4">
      <div className="flex items-center justify-between gap-2.5">
        <Eyebrow className="normal-case tracking-normal text-[13px] font-semibold text-foreground">
          Saúde do negócio
          <InfoTip text="Fôlego: quantos dias o caixa segura se nada novo entrar, já contando tudo que está agendado pra sair. Verde: mais de 30 dias · âmbar: 15 a 30 · vermelho: menos de 15." />
        </Eyebrow>
        <span className={cn('inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold', VERDICT_CLASS[vm.verdict])}>
          <span className="h-1.5 w-1.5 rounded-full bg-current" />
          {VERDICT_LABEL[vm.verdict]}
        </span>
      </div>

      <button
        type="button"
        onClick={() => onDrill({ rota: '/financeiro/fluxo-de-caixa', mensagem: '→ Fluxo de caixa — projeção dia a dia e fôlego' })}
        aria-label={`Fôlego de caixa: ${vm.diasFolego} dias, zona ${VERDICT_LABEL[vm.verdict].toLowerCase()}. Abrir Fluxo de caixa.`}
        className="relative mx-auto mt-0.5 block w-[236px] max-w-full rounded-2xl pt-1 transition-colors hover:bg-surface-2/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <GaugeSvg diasFolego={vm.diasFolego} />
        <div className="pointer-events-none absolute inset-x-0 top-[46px] text-center">
          <div className="num text-[44px] font-extrabold leading-none tracking-tight text-foreground">{vm.diasFolego}</div>
          <div className="mt-[3px] text-[11.5px] font-semibold text-muted-foreground">dias de fôlego</div>
        </div>
      </button>

      <div className="mt-auto flex gap-2.5 pt-3.5">
        <button
          type="button"
          onClick={() => onDrill({ rota: '/financeiro/bancario', mensagem: '→ Bancário — saldo por conta (banco + gaveta)' })}
          className="min-w-0 flex-1 rounded-xl bg-surface-2 px-3.5 py-2.5 text-left transition-transform hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <div className="text-[11px] font-semibold text-muted-foreground">Em caixa</div>
          <MoneyValue centavos={vm.emCaixaCentavos} className="mt-0.5 block text-[15px] font-bold" />
        </button>
        <button
          type="button"
          onClick={() => onDrill({ rota: '/financeiro/entradas-saidas', mensagem: '→ Entradas & Saídas — o que já tem dono' })}
          className="min-w-0 flex-1 rounded-xl bg-surface-2 px-3.5 py-2.5 text-left transition-transform hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <div className="text-[11px] font-semibold text-muted-foreground">Pode tirar</div>
          <div className="mt-0.5 flex items-baseline gap-1">
            <MoneyValue centavos={vm.podeTirarCentavos} className="text-[15px] font-bold" />
            {vm.podeTirarCentavos >= 0 && <span className="text-xs font-bold text-pos">✓</span>}
          </div>
        </button>
      </div>
    </Surface>
  );
}
