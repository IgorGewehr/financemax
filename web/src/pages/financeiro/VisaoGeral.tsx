import { motion } from 'framer-motion';
import { Wallet } from 'lucide-react';

import { GaugeCard } from '@/components/financial/visao-geral/GaugeCard';
import { LancarFab } from '@/components/financial/visao-geral/LancarFab';
import { MixCard } from '@/components/financial/visao-geral/MixCard';
import { PeriodoTrigger } from '@/components/financial/visao-geral/PeriodoTrigger';
import { RoiMiniCard } from '@/components/financial/visao-geral/RoiMiniCard';
import { SimplesMiniCard } from '@/components/financial/visao-geral/SimplesMiniCard';
import { Tiles } from '@/components/financial/visao-geral/Tiles';
import { TimelineCard } from '@/components/financial/visao-geral/TimelineCard';
import { useDrillNav } from '@/components/financial/visao-geral/useDrillNav';
import { useVisaoGeralV3 } from '@/components/financial/visao-geral/useVisaoGeralV3';
import { PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

const PERIODO_LABEL = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(new Date());

function BlocoQuebrado({ mensagem, className }: { mensagem: string; className?: string }) {
  return (
    <Surface padding="lg" className={cn('h-full', className)}>
      <EmptyState icon={<Wallet className="h-5 w-5" />} title="Não deu para carregar" description={mensagem} className="border-none py-6" />
    </Surface>
  );
}

/**
 * Visão Geral v3 — 1:1 com `docs/ui/mockups/visao-geral-v3.html` (financemax): gauge de fôlego +
 * projeção de caixa + 4 tiles + mix de receita + ROI/Simples. Substitui a v1/v2 (hero +
 * decomposição + Super Consultor) inteira — ver `useVisaoGeralV3` pro mapeamento DTO → tela.
 * SEM título "Visão Geral" (a aba do `FinanceiroLayout` já nomeia a seção — `PageHeader` nunca
 * repete), SEM Super Consultor, SEM botão de opt-in flutuante: o cartão "Investimento" só existe
 * quando `imobilizadoRoiAtivo` está ligado em Financeiro › Configurações.
 */
export function VisaoGeral() {
  const vm = useVisaoGeralV3();
  const drill = useDrillNav();

  return (
    <div>
      <PageHeader subtitle="Bateu o olho, entendeu." actions={<PeriodoTrigger label={PERIODO_LABEL} />} />

      <div className="mb-3.5 grid grid-cols-1 items-stretch gap-3.5 lg:grid-cols-[340px_1fr]">
        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.02 }}>
          {vm.gauge.carregando ? (
            <Surface padding="lg" className="h-full min-h-[300px]">
              <Skeleton className="mx-auto h-40 w-40 rounded-full" />
              <Skeleton className="mt-4 h-10 w-full" />
            </Surface>
          ) : vm.gauge.erro || !vm.gauge.dado ? (
            <BlocoQuebrado mensagem={vm.gauge.erro ?? ''} className="min-h-[300px]" />
          ) : (
            <GaugeCard vm={vm.gauge.dado} onDrill={drill} />
          )}
        </motion.div>

        <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.06 }}>
          {vm.timeline.carregando ? (
            <Surface padding="lg" className="h-full min-h-[300px]">
              <Skeleton className="h-4 w-56" />
              <Skeleton className="mt-6 h-[200px] w-full" />
            </Surface>
          ) : vm.timeline.erro || !vm.timeline.dado ? (
            <BlocoQuebrado mensagem={vm.timeline.erro ?? ''} className="min-h-[300px]" />
          ) : (
            <TimelineCard vm={vm.timeline.dado} />
          )}
        </motion.div>
      </div>

      <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.42, delay: 0.1 }}>
        {vm.tiles.carregando ? (
          <div className="mb-3.5 grid grid-cols-1 gap-3.5 sm:grid-cols-2 xl:grid-cols-4">
            {[0, 1, 2, 3].map((i) => (
              <Surface key={i} padding="lg" className="min-h-[130px]">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="mt-3 h-7 w-24" />
                <Skeleton className="mt-3 h-5 w-full" />
              </Surface>
            ))}
          </div>
        ) : vm.tiles.erro || !vm.tiles.dado ? (
          <BlocoQuebrado mensagem={vm.tiles.erro ?? ''} className="mb-3.5 min-h-[130px]" />
        ) : (
          <Tiles vm={vm.tiles.dado} onDrill={drill} />
        )}
      </motion.div>

      <motion.div
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.42, delay: 0.14 }}
        className={cn('grid grid-cols-1 gap-3.5 xl:grid-cols-2', vm.roiHabilitado !== false && 'xl:grid-cols-[1.3fr_1fr_1fr]')}
      >
        {vm.mix.carregando ? (
          <Surface padding="lg" className="min-h-[150px]">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="mt-4 h-24 w-full" />
          </Surface>
        ) : vm.mix.erro || !vm.mix.dado ? (
          <BlocoQuebrado mensagem={vm.mix.erro ?? ''} className="min-h-[150px]" />
        ) : (
          <MixCard vm={vm.mix.dado} onDrill={drill} />
        )}

        {vm.roiHabilitado !== false &&
          (vm.roiMini.carregando ? (
            <Surface padding="lg" className="min-h-[150px]">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="mt-4 h-7 w-20" />
            </Surface>
          ) : vm.roiMini.dado ? (
            <RoiMiniCard vm={vm.roiMini.dado} onDrill={drill} />
          ) : null)}

        {vm.simplesMini.carregando ? (
          <Surface padding="lg" className="min-h-[150px]">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="mt-4 h-7 w-20" />
          </Surface>
        ) : vm.simplesMini.erro || !vm.simplesMini.dado ? (
          <BlocoQuebrado mensagem={vm.simplesMini.erro ?? ''} className="min-h-[150px]" />
        ) : (
          <SimplesMiniCard vm={vm.simplesMini.dado} onDrill={drill} />
        )}
      </motion.div>

      <LancarFab />
    </div>
  );
}
