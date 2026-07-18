import { AbertoCard } from './AbertoCard';
import { DreCard } from './DreCard';
import { MrrCard } from './MrrCard';
import type { AbertoViewModel, DreRegimeBlock, MrrViewModel } from './types';

interface DocGridProps {
  dre: DreRegimeBlock;
  periodLabel: string;
  aberto: AbertoViewModel;
  mrr: MrrViewModel;
}

/** Grid de cards de relatório — 1 coluna empilhada até 900px, 3 colunas acima disso. */
export function DocGrid({ dre, periodLabel, aberto, mrr }: DocGridProps) {
  return (
    <section className="mb-4.5 grid grid-cols-1 gap-4 sm:grid-cols-2 min-[980px]:grid-cols-3">
      <DreCard dre={dre} periodLabel={periodLabel} />
      <AbertoCard aberto={aberto} />
      <MrrCard mrr={mrr} />
    </section>
  );
}
