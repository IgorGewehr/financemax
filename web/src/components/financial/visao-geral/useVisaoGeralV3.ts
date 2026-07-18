import { useCallback, useEffect, useState } from 'react';

import {
  deGaugeDto,
  deMixDto,
  deRoiMiniDto,
  deSimplesMiniDto,
  deTileAPagar,
  deTileAReceber,
  deTileAssinaturas,
  deTileResultado,
  deTimelineDto,
} from '@/lib/api/adapters/financeiro/visaoGeralV3';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';
import { addDays, endOfMonthIso, startOfMonthIso, todayIso } from '@/lib/date';

import type { GaugeViewModel, MixViewModel, RoiMiniViewModel, SimplesMiniViewModel, TilesViewModel, TimelineViewModel } from './types';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function inicial<T>(): Recurso<T> {
  return { dado: null, erro: null, carregando: true };
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar.';
}

/**
 * Estado de dado REAL da Visão Geral v3 — um `Recurso<T>` por bloco (mesmo padrão do
 * `useVisaoGeral` da v1/v2): um bloco quebrado não derruba os outros. `roiMini` é o único
 * condicional — 404 quando `imobilizadoRoiAtivo` está desligado (`useFinanceiroConfiguracoes`)
 * vira "sem dado" silencioso, não erro (a Visão Geral v3 nunca renderiza o cartão de Investimento
 * pra tenant com o opt-in desligado, ver `VisaoGeral.tsx`).
 */
export function useVisaoGeralV3() {
  const [gauge, setGauge] = useState<Recurso<GaugeViewModel>>(inicial);
  const [timeline, setTimeline] = useState<Recurso<TimelineViewModel>>(inicial);
  const [tiles, setTiles] = useState<Recurso<TilesViewModel>>(inicial);
  const [mix, setMix] = useState<Recurso<MixViewModel>>(inicial);
  const [roiMini, setRoiMini] = useState<Recurso<RoiMiniViewModel | null>>(inicial);
  const [simplesMini, setSimplesMini] = useState<Recurso<SimplesMiniViewModel>>(inicial);
  const [roiHabilitado, setRoiHabilitado] = useState<boolean | null>(null);

  const carregar = useCallback(() => {
    setGauge(inicial());
    setTimeline(inicial());
    setTiles(inicial());
    setMix(inicial());
    setRoiMini(inicial());
    setSimplesMini(inicial());

    const hoje = todayIso();
    const mesAtual = { de: startOfMonthIso(hoje), ate: endOfMonthIso(hoje) };
    const fimDoMesAnterior = addDays(mesAtual.de, -1);
    const mesAnterior = { de: startOfMonthIso(fimDoMesAnterior), ate: fimDoMesAnterior };

    Promise.all([financeiroApi.previsaoCaixa(), financeiroApi.disponivelParaRetirada()])
      .then(([previsao, disponivel]) => setGauge({ dado: deGaugeDto(previsao, disponivel), erro: null, carregando: false }))
      .catch((e) => setGauge({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .fluxo(14, 30)
      .then((dto) => setTimeline({ dado: deTimelineDto(dto), erro: null, carregando: false }))
      .catch((e) => setTimeline({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    Promise.all([
      financeiroApi.relatoriosContasEmAberto(),
      financeiroApi.extrato(hoje, addDays(hoje, 30), 'saida'),
      financeiroApi.relatoriosDre(mesAtual.de, mesAtual.ate),
      financeiroApi.relatoriosDre(mesAnterior.de, mesAnterior.ate),
      financeiroApi.fatoReceitaDiaria(mesAtual.de, hoje),
      financeiroApi.receitaRecorrente(),
    ])
      .then(([contasEmAberto, extratoSaida, dreAtual, dreAnterior, fatosReceita, receitaRecorrente]) => {
        const linhasEmAberto = extratoSaida.linhas.filter((l) => l.status !== 'pago');
        const serieReceita = fatosReceita.map((f) => f.receitaCentavos);

        setTiles({
          dado: {
            aReceber: deTileAReceber(contasEmAberto),
            aPagar: deTileAPagar(contasEmAberto, linhasEmAberto, hoje),
            resultado: deTileResultado(dreAtual, dreAnterior.resultadoOperacional.centavos, serieReceita),
            assinaturas: deTileAssinaturas(receitaRecorrente),
          },
          erro: null,
          carregando: false,
        });
        setMix({ dado: deMixDto(dreAtual), erro: null, carregando: false });
      })
      .catch((e) => {
        setTiles({ dado: null, erro: mensagemDeErro(e), carregando: false });
        setMix({ dado: null, erro: mensagemDeErro(e), carregando: false });
      });

    financeiroApi
      .configuracoes()
      .then((config) => {
        setRoiHabilitado(config.imobilizadoRoiAtivo);
        if (!config.imobilizadoRoiAtivo) {
          setRoiMini({ dado: null, erro: null, carregando: false });
          return;
        }
        financeiroApi
          .roiNegocio()
          .then((dto) => setRoiMini({ dado: deRoiMiniDto(dto), erro: null, carregando: false }))
          .catch((e) => setRoiMini({ dado: null, erro: mensagemDeErro(e), carregando: false }));
      })
      .catch((e) => {
        setRoiHabilitado(false);
        setRoiMini({ dado: null, erro: mensagemDeErro(e), carregando: false });
      });

    financeiroApi
      .radarSimples()
      .then((dto) => setSimplesMini({ dado: deSimplesMiniDto(dto), erro: null, carregando: false }))
      .catch((e) => setSimplesMini({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  return { gauge, timeline, tiles, mix, roiMini, roiHabilitado, simplesMini, recarregar: carregar };
}

export type VisaoGeralV3Vm = ReturnType<typeof useVisaoGeralV3>;
