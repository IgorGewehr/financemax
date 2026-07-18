import { SectionCard } from '@/components/shared';

import { formatCentavosWhole } from './money';
import type { Atrasados30DiasResumo } from './types';

interface RaioXDoMesProps {
  atrasados30: Atrasados30DiasResumo;
  onClickAtrasados: () => void;
}

/** "Atrasados há mais de 30 dias" — único recorte de "Raio-X do mês" com read-model real (dado
 * REAL, derivado da Linha do tempo). "Fixo × variável" e "Quem mais subiu" saíram: dependiam de
 * quebra por categoria em 6 meses, que o domínio ainda não expõe. */
export function RaioXDoMes({ atrasados30, onClickAtrasados }: RaioXDoMesProps) {
  return (
    <SectionCard title="Raio-X do mês">
      <div className="px-4 pb-4 pt-1">
        <button
          type="button"
          onClick={onClickAtrasados}
          className="w-full rounded-xl bg-surface-2 px-3.5 py-3 text-left transition-[filter] hover:brightness-[0.97] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95 dark:hover:brightness-125"
        >
          <div className="text-xs font-semibold text-muted-foreground">Atrasados há mais de 30 dias</div>
          <div className="num mt-1 text-[23px] font-bold tracking-tight text-crit">
            {formatCentavosWhole(atrasados30.totalCentavos)}{' '}
            <span className="font-sans text-[13px] font-semibold text-muted-foreground">· {atrasados30.qtdClientes} clientes</span>
          </div>
          <div className="mt-0.5 text-xs text-faint">clique para ver na linha do tempo →</div>
        </button>
      </div>
    </SectionCard>
  );
}
