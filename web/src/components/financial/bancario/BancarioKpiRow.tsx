import { ArrowUpRight } from 'lucide-react';
import { useId } from 'react';

import { KpiCard } from '@/components/shared';
import type { Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { BancarioMoneyValue } from './BancarioMoneyValue';
import { saldoTotalContas, formatSaldoFoot, mesEntrouTotal, mesSaiuTotal, computeSaldoSparkline } from './derive';
import { formatCentavosWhole } from './money';
import type { ContaBancaria, KpiDeltaExemplo, SemanaMovimento } from './types';

interface BancarioKpiRowProps {
  contas: ContaBancaria[];
  semanas: SemanaMovimento[];
  kpiSaldoDelta: KpiDeltaExemplo;
  kpiEntrouDelta: KpiDeltaExemplo;
  kpiEntrouFoot: string;
  kpiSaiuDelta: KpiDeltaExemplo;
  kpiSaiuFoot: string;
  /** Vem do estado vivo de conciliação (sobe/desce conforme o usuário resolve itens). */
  conciliarCount: number;
  conciliarTotalCentavos: Centavos;
}

/** Seta de delta — o mockup usa o MESMO ícone (seta pra cima-direita) em alta e em baixa, só a cor muda. */
function KpiDelta({ delta }: { delta: KpiDeltaExemplo }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 text-[12.5px] font-semibold',
        delta.direcao === 'up' ? 'text-pos' : 'text-crit',
      )}
    >
      <ArrowUpRight className="h-3.5 w-3.5" strokeWidth={2.5} />
      {delta.label}
    </span>
  );
}

/**
 * Sparkline do KPI hero — traçado REAL, reconstruído do saldo atual das contas + o líquido de
 * cada semana carregada (ver `computeSaldoSparkline`). Sem semana nenhuma, cai numa reta neutra.
 */
function SaldoSparkline({ contas, semanas }: { contas: ContaBancaria[]; semanas: SemanaMovimento[] }) {
  const gradientId = useId();
  const { linePath, areaPath, lastPoint } = computeSaldoSparkline(contas, semanas);
  return (
    <svg
      viewBox="0 0 260 34"
      preserveAspectRatio="none"
      aria-hidden="true"
      className="mt-2.5 block h-[34px] w-full text-primary-600"
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="currentColor" stopOpacity="0.28" />
          <stop offset="1" stopColor="currentColor" stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={linePath} fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" />
      <path d={areaPath} fill={`url(#${gradientId})`} stroke="none" />
      <circle cx={lastPoint.x} cy={lastPoint.y} r={3} fill="currentColor" />
    </svg>
  );
}

/** As 4 KPIs do topo do Bancário — saldo consolidado (hero), entrou, saiu, e a conciliar (vivo). */
export function BancarioKpiRow({
  contas,
  semanas,
  kpiSaldoDelta,
  kpiEntrouDelta,
  kpiEntrouFoot,
  kpiSaiuDelta,
  kpiSaiuFoot,
  conciliarCount,
  conciliarTotalCentavos,
}: BancarioKpiRowProps) {
  const saldoTotal = saldoTotalContas(contas);
  const entrouTotal = mesEntrouTotal(semanas);
  const saiuTotal = mesSaiuTotal(semanas);
  const saldoFoot = formatSaldoFoot(contas, formatCentavosWhole);
  const conciliado = conciliarCount === 0;

  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 sm:grid-cols-4">
      <KpiCard hero label="Saldo em bancos" value={<BancarioMoneyValue centavos={saldoTotal} />} foot={<KpiDelta delta={kpiSaldoDelta} />}>
        <SaldoSparkline contas={contas} semanas={semanas} />
        <div className="mt-1 text-xs text-muted-foreground">{saldoFoot}</div>
      </KpiCard>

      <KpiCard
        label="Entrou no mês"
        value={<BancarioMoneyValue centavos={entrouTotal} />}
        foot={<KpiDelta delta={kpiEntrouDelta} />}
      >
        <div className="mt-1 text-xs text-muted-foreground">{kpiEntrouFoot}</div>
      </KpiCard>

      <KpiCard label="Saiu no mês" value={<BancarioMoneyValue centavos={saiuTotal} />} foot={<KpiDelta delta={kpiSaiuDelta} />}>
        <div className="mt-1 text-xs text-muted-foreground">{kpiSaiuFoot}</div>
      </KpiCard>

      <KpiCard
        label="A conciliar"
        value={
          conciliado ? (
            <>
              <span className="num text-pos">0</span> <span className="text-[15px] font-semibold text-pos">itens ✔</span>
            </>
          ) : (
            <>
              <span className="num text-warn">{conciliarCount}</span>{' '}
              <span className="text-[15px] font-semibold text-warn">itens ⚠</span>
            </>
          )
        }
        foot={conciliado ? <span className="text-pos">Tudo conciliado</span> : `${formatCentavosWhole(conciliarTotalCentavos)} em dúvida`}
      />
    </section>
  );
}
