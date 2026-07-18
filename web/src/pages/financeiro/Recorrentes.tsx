import { Plus, Repeat } from 'lucide-react';
import { useState } from 'react';

import { AssinaturasTabelaReal } from '@/components/financial/recorrentes/AssinaturasTabelaReal';
import { AssinResumoReal } from '@/components/financial/recorrentes/AssinResumoReal';
import { FixasTabelaReal } from '@/components/financial/recorrentes/FixasTabelaReal';
import { LensSwitch } from '@/components/financial/recorrentes/LensSwitch';
import type { LenteRecorrentes } from '@/components/financial/recorrentes/types';
import { useAssinaturasDetalhe } from '@/components/financial/recorrentes/useAssinaturasDetalhe';
import { useContasFixasReal } from '@/components/financial/recorrentes/useContasFixasReal';
import { useReceitaRecorrente } from '@/components/financial/recorrentes/useReceitaRecorrente';
import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { useToast } from '@/lib/toast';

const LENS_COPY: Record<LenteRecorrentes, { subtitulo: string; botao: string }> = {
  fixas: {
    subtitulo: 'O que custa existir todo mês — antes de vender qualquer coisa.',
    botao: 'Nova recorrência',
  },
  assinaturas: {
    subtitulo: 'Seus softwares, os clientes que pagam por eles, e o que isso vale de verdade.',
    botao: 'Nova assinatura',
  },
};

/**
 * Financeiro › Recorrentes — página fina: só compõe as seções por lente. Todo dado exibido é REAL:
 * o resumo agregado de Assinaturas (MRR/ARR/ticket médio) e as duas tabelas nominais "Todas as ..."
 * — ver `useReceitaRecorrente`/`useAssinaturasDetalhe`/`useContasFixasReal`. O retrato analítico com
 * histórico de 6/12 meses ("Retrato do fixo", MRR por serviço, retenção da carteira) foi removido —
 * dependia de um cruzamento Recorrência×histórico que o domínio ainda não expõe (ver
 * docs/wiring/financeiro-telas-restantes.md §2).
 */
export function Recorrentes() {
  const [lens, setLens] = useState<LenteRecorrentes>('fixas');
  const { toast } = useToast();
  const copy = LENS_COPY[lens];
  const resumoReal = useReceitaRecorrente();
  const assinaturasDetalhe = useAssinaturasDetalhe();
  const fixasReal = useContasFixasReal();

  return (
    <div>
      <PageHeader
        subtitle={copy.subtitulo}
        actions={
          <Button
            icon={<Plus className="h-[15px] w-[15px]" strokeWidth={2.4} />}
            onClick={() => toast(`Cadastro de ${lens === 'fixas' ? 'nova recorrência' : 'nova assinatura'} — formulário aberto.`, 'info')}
          >
            {copy.botao}
          </Button>
        }
      />

      <LensSwitch lens={lens} onChange={setLens} />

      {lens === 'fixas' ? (
        fixasReal.carregando ? (
          <Surface padding="lg" className="mb-4 min-h-[160px]">
            <Skeleton className="h-32 w-full" />
          </Surface>
        ) : fixasReal.erro || !fixasReal.dado ? (
          <Surface padding="lg" className="mb-4">
            <EmptyState
              icon={<Repeat className="h-5 w-5" />}
              title="Não deu para carregar as recorrências"
              description={fixasReal.erro ?? ''}
              className="border-none py-6"
            />
          </Surface>
        ) : (
          <FixasTabelaReal itens={fixasReal.dado} />
        )
      ) : (
        <>
          <AssinResumoReal recurso={resumoReal} />
          {assinaturasDetalhe.carregando ? (
            <Surface padding="lg" className="mb-4 min-h-[160px]">
              <Skeleton className="h-32 w-full" />
            </Surface>
          ) : assinaturasDetalhe.erro || !assinaturasDetalhe.dado ? (
            <Surface padding="lg" className="mb-4">
              <EmptyState
                icon={<Repeat className="h-5 w-5" />}
                title="Não deu para carregar as assinaturas"
                description={assinaturasDetalhe.erro ?? ''}
                className="border-none py-6"
              />
            </Surface>
          ) : (
            <AssinaturasTabelaReal itens={assinaturasDetalhe.dado} />
          )}
        </>
      )}
    </div>
  );
}
