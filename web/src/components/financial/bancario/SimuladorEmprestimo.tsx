import { Calculator } from 'lucide-react';
import { useId, useState } from 'react';

import { SectionCard, StatusChip, type ChipTone } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { ApiError } from '@/lib/api/client';
import { financeiroApi, type SimulacaoEmprestimoDto, type VereditoEmprestimo } from '@/lib/api/financeiro';
import { reais } from '@/lib/money';

import { BancarioMoneyValue } from './BancarioMoneyValue';

const VEREDITO_LABEL: Record<VereditoEmprestimo, string> = {
  viavel: 'Viável',
  apertado: 'Apertado',
  inviavel: 'Inviável',
};

const VEREDITO_TONE: Record<VereditoEmprestimo, ChipTone> = {
  viavel: 'sobra',
  apertado: 'aberto',
  inviavel: 'falta',
};

interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível simular.';
}

/**
 * Simulação de empréstimo do Bancário — o dono informa os termos e simula localmente; não há
 * side-effect nem persistência (Lei 2 não se aplica: é o usuário simulando, não a IA agindo).
 * O cálculo (Tabela Price + taxa efetiva anual + veredito de viabilidade) roda no servidor
 * (`POST /financeiro/bancario/simular-emprestimo`) pra manter a mesma fonte de verdade usada em
 * outras superfícies do produto.
 */
export function SimuladorEmprestimo() {
  const [valor, setValor] = useState('');
  const [taxaMensal, setTaxaMensal] = useState('');
  const [prazoMeses, setPrazoMeses] = useState('');
  const [retornoMensal, setRetornoMensal] = useState('');
  const [resultado, setResultado] = useState<Recurso<SimulacaoEmprestimoDto>>({ dado: null, erro: null, carregando: false });

  const valorNum = Number(valor);
  const taxaNum = Number(taxaMensal);
  const prazoNum = Number(prazoMeses);
  const formValido = valorNum > 0 && taxaNum >= 0 && prazoNum > 0;

  const valorId = useId();
  const taxaId = useId();
  const prazoId = useId();
  const retornoId = useId();

  function simular() {
    if (!formValido) return;
    setResultado({ dado: null, erro: null, carregando: true });
    financeiroApi
      .simularEmprestimo({
        valorCentavos: reais(valorNum),
        taxaJurosMensalBps: Math.round(taxaNum * 100),
        prazoMeses: prazoNum,
        retornoMensalEsperadoCentavos: retornoMensal ? reais(Number(retornoMensal)) : null,
      })
      .then((dto) => setResultado({ dado: dto, erro: null, carregando: false }))
      .catch((e: unknown) => setResultado({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }

  return (
    <SectionCard title="Simulação de empréstimo" hint="valor, taxa e prazo — sem compromisso" className="mb-4">
      <div className="grid grid-cols-1 gap-5 p-4 sm:p-[18px] lg:grid-cols-[1fr_1.1fr]">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            simular();
          }}
          className="flex flex-col gap-3"
        >
          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <label htmlFor={valorId} className="text-xs font-semibold text-muted-foreground">
                Valor do empréstimo (R$)
              </label>
              <input
                id={valorId}
                type="number"
                min={0}
                step={1}
                value={valor}
                onChange={(e) => setValor(e.target.value)}
                placeholder="0"
                className="num rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor={taxaId} className="text-xs font-semibold text-muted-foreground">
                Juros ao mês (%)
              </label>
              <input
                id={taxaId}
                type="number"
                min={0}
                step={0.01}
                value={taxaMensal}
                onChange={(e) => setTaxaMensal(e.target.value)}
                placeholder="0,00"
                className="num rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <label htmlFor={prazoId} className="text-xs font-semibold text-muted-foreground">
                Prazo (meses)
              </label>
              <input
                id={prazoId}
                type="number"
                min={1}
                step={1}
                value={prazoMeses}
                onChange={(e) => setPrazoMeses(e.target.value)}
                placeholder="0"
                className="num rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor={retornoId} className="text-xs font-semibold text-muted-foreground">
                Retorno mensal do equip. (R$)
              </label>
              <input
                id={retornoId}
                type="number"
                min={0}
                step={1}
                value={retornoMensal}
                onChange={(e) => setRetornoMensal(e.target.value)}
                placeholder="opcional"
                className="num rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
          </div>

          <Button type="submit" variant="primary" size="md" disabled={!formValido || resultado.carregando} className="mt-1 justify-center">
            <Calculator className="h-4 w-4" strokeWidth={2.4} />
            {resultado.carregando ? 'Simulando…' : 'Simular'}
          </Button>
        </form>

        <div className="rounded-xl bg-surface-2 p-3.5 sm:p-4">
          {!resultado.dado && !resultado.erro && !resultado.carregando && (
            <p className="text-sm text-muted-foreground">Preencha os termos e clique em "Simular" pra ver parcela, custo total e viabilidade.</p>
          )}

          {resultado.carregando && <p className="text-sm text-muted-foreground">Calculando…</p>}

          {resultado.erro && <p className="text-sm text-crit">Não deu para simular: {resultado.erro}</p>}

          {resultado.dado && (
            <div className="flex flex-col gap-3">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Veredito</span>
                <StatusChip tone={VEREDITO_TONE[resultado.dado.veredito]}>{VEREDITO_LABEL[resultado.dado.veredito]}</StatusChip>
              </div>
              <p className="text-[13px] leading-relaxed text-foreground">{resultado.dado.motivo}</p>

              <div className="mt-1 grid grid-cols-2 gap-x-4 gap-y-3 border-t border-border pt-3">
                <div>
                  <div className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Parcela mensal</div>
                  <BancarioMoneyValue centavos={resultado.dado.parcelaMensalCentavos} className="mt-0.5 block text-lg font-bold" />
                </div>
                <div>
                  <div className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Custo total</div>
                  <BancarioMoneyValue centavos={resultado.dado.custoTotalCentavos} className="mt-0.5 block text-lg font-bold" />
                </div>
                <div>
                  <div className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Juros totais</div>
                  <BancarioMoneyValue centavos={resultado.dado.jurosTotaisCentavos} tone="crit" className="mt-0.5 block text-lg font-bold" />
                </div>
                <div>
                  <div
                    className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground"
                    title="Capitalização composta do juro mensal informado — não inclui IOF, tarifas ou seguro, por isso não é rotulada como CET (Custo Efetivo Total)."
                  >
                    Taxa efetiva anual
                  </div>
                  <div className="num mt-0.5 text-lg font-bold text-foreground">
                    {resultado.dado.taxaEfetivaAnualPercent.toFixed(2).replace('.', ',')}%
                  </div>
                </div>
                {resultado.dado.paybackMeses !== null && (
                  <div className="col-span-2">
                    <div className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Payback do equipamento</div>
                    <div className="num mt-0.5 text-lg font-bold text-foreground">
                      {resultado.dado.paybackMeses} {resultado.dado.paybackMeses === 1 ? 'mês' : 'meses'}
                    </div>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </SectionCard>
  );
}
