import { AlertTriangle } from 'lucide-react';

import { DocGrid } from '@/components/financial/relatorios/DocGrid';
import { InfoNote } from '@/components/financial/relatorios/InfoNote';
import { useRelatoriosReais } from '@/components/financial/relatorios/useRelatoriosReais';
import { PageHeader } from '@/components/shared';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { todayIso } from '@/lib/date';

const BLOCO_LABEL: Record<'mrr' | 'aberto' | 'dreCompetencia', string> = {
  mrr: 'MRR',
  aberto: 'Contas em aberto',
  dreCompetencia: 'DRE (competência)',
};

const MESES_PT = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

/** "2026-07-16" → "Julho 2026". */
function periodoLabelAtual(): string {
  const [ano, mes] = todayIso().split('-');
  return `${MESES_PT[Number(mes) - 1]} ${ano}`;
}

/**
 * Financeiro › Relatórios — página fina, só compõe as seções. MRR, Contas em aberto e DRE
 * (regime de competência) são dado REAL — ver `useRelatoriosReais`. Regime de caixa do DRE, geração
 * de PDF/Excel, pacote de fechamento (.zip), envio por e-mail/WhatsApp e histórico de exports saíram
 * da tela: nenhum tinha serviço/persistência real no backend (eram só simulação de UI, ver
 * docs/wiring/financeiro-telas-restantes.md §5).
 */
export function Relatorios() {
  const reais = useRelatoriosReais();
  const periodLabel = periodoLabelAtual();

  const carregando = reais.mrr.carregando || reais.aberto.carregando || reais.dreCompetencia.carregando;

  const erros = (['mrr', 'aberto', 'dreCompetencia'] as const)
    .filter((chave) => !reais[chave].carregando && (reais[chave].erro || !reais[chave].dado))
    .map((chave) => ({ chave, mensagem: reais[chave].erro ?? 'Sem dado.' }));

  return (
    <div>
      <PageHeader
        subtitle="Documentos prontos pra mandar pro seu contador ou sócios."
        actions={
          <div className="flex items-center gap-2 rounded-[10px] border border-border bg-card px-3 py-2 text-[13px] font-semibold text-foreground">
            {periodLabel}
          </div>
        }
      />

      <InfoNote />

      {!carregando && erros.length > 0 && (
        <Surface padding="lg" className="mb-4.5 border-crit/40 bg-crit-soft/40">
          <div className="flex items-start gap-2.5 text-sm text-crit">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <div>
              <p className="font-semibold">Não deu para carregar {erros.map((e) => BLOCO_LABEL[e.chave]).join(', ')}.</p>
              <ul className="mt-1 space-y-0.5 text-xs text-crit/80">
                {erros.map((e) => (
                  <li key={e.chave}>
                    {BLOCO_LABEL[e.chave]}: {e.mensagem}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </Surface>
      )}

      {carregando ? (
        <Surface padding="lg" className="mb-4.5 min-h-[280px]">
          <Skeleton className="h-56 w-full" />
        </Surface>
      ) : (
        reais.dreCompetencia.dado &&
        reais.aberto.dado &&
        reais.mrr.dado && (
          <DocGrid dre={reais.dreCompetencia.dado} periodLabel={periodLabel} aberto={reais.aberto.dado} mrr={reais.mrr.dado} />
        )
      )}
    </div>
  );
}
