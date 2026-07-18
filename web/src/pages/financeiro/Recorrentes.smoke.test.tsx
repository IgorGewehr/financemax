// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ContaFixaResumoDto, ReceitaRecorrenteDto, RecorrenteDetalheDto } from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { Recorrentes } from './Recorrentes';

const recorrentesFixas = vi.fn();
const receitaRecorrente = vi.fn();
const recorrentesDetalhe = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    recorrentesFixas: (...args: unknown[]) => recorrentesFixas(...args),
    receitaRecorrente: (...args: unknown[]) => receitaRecorrente(...args),
    recorrentesDetalhe: (...args: unknown[]) => recorrentesDetalhe(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const FIXA: ContaFixaResumoDto = {
  id: 'f1',
  descricao: 'Aluguel',
  categoriaId: 'cat1',
  valorPrevisto: money(350000),
  diaFixo: 5,
  frequencia: 'mensal',
  tipo: 'despesa',
  proximaOcorrencia: '2026-08-05',
};

const RESUMO: ReceitaRecorrenteDto = {
  mrr: money(500000),
  arr: money(6000000),
  assinaturasAtivas: 3,
  ticketMedio: money(166666),
  mrrNovoNoMes: money(50000),
  mrrChurnNoMes: money(0),
  clientesChurnNoMes: 0,
  churnPercent: 0,
  porServico: [],
  maiorConcentracao: null,
};

const ASSINATURA: RecorrenteDetalheDto = {
  id: 'a1',
  clienteId: 'cl1',
  clienteNome: 'Padaria Bela Vista',
  servicoId: 's1',
  servicoNome: 'ServicePro Pro',
  valorPorCiclo: money(29900),
  ciclo: 'mensal',
  status: 'ativa',
  proximaCobranca: '2026-08-01',
};

function renderComToast() {
  return render(
    <ToastProvider>
      <Recorrentes />
    </ToastProvider>,
  );
}

describe('Financeiro › Recorrentes (smoke)', () => {
  beforeEach(() => {
    recorrentesFixas.mockReset().mockResolvedValue([FIXA]);
    receitaRecorrente.mockReset().mockResolvedValue(RESUMO);
    recorrentesDetalhe.mockReset().mockResolvedValue([ASSINATURA]);
  });

  afterEach(() => cleanup());

  it('lente Contas fixas: carrega e mostra o template real (não trava em skeleton)', async () => {
    renderComToast();

    await waitFor(() => expect(screen.getByText('Aluguel')).toBeInTheDocument());
    expect(recorrentesFixas).toHaveBeenCalled();
  });

  it('troca para a lente Assinaturas: resumo agregado + tabela nominal real', async () => {
    renderComToast();

    await waitFor(() => expect(screen.getByText('Aluguel')).toBeInTheDocument());

    fireEvent.click(screen.getByText('Assinaturas'));

    await waitFor(() => expect(screen.getByText('Padaria Bela Vista')).toBeInTheDocument());
    expect(receitaRecorrente).toHaveBeenCalled();
    expect(recorrentesDetalhe).toHaveBeenCalled();
  });

  it('erro ao carregar as recorrências fixas mostra estado de erro — não finge lista vazia', async () => {
    recorrentesFixas.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    renderComToast();

    await waitFor(() => expect(screen.getByText('Não deu para carregar as recorrências')).toBeInTheDocument());
    expect(screen.queryByText('Aluguel')).not.toBeInTheDocument();
  });
});
