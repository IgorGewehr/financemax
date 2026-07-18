// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ContaBancariaDto, SessaoCaixaDto } from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { FluxoCaixa } from './FluxoCaixa';

const caixaAtual = vi.fn();
const caixaHistorico = vi.fn();
const contasBancarias = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    caixaAtual: (...args: unknown[]) => caixaAtual(...args),
    caixaHistorico: (...args: unknown[]) => caixaHistorico(...args),
    contasBancarias: (...args: unknown[]) => contasBancarias(...args),
    abrirCaixa: vi.fn(),
    caixaSuprimento: vi.fn(),
    caixaSangria: vi.fn(),
    caixaFechar: vi.fn(),
  },
}));

const CONTAS: ContaBancariaDto[] = [{ id: 'c1', nome: 'Itaú', tipo: 'corrente', saldo: { centavos: 500000, moeda: 'BRL' }, ativa: true }];

const SESSAO_ABERTA: SessaoCaixaDto = {
  id: 's1',
  contaCaixaId: 'caixa1',
  operadorId: 'joao',
  operadorNome: 'João',
  status: 'Aberta',
  abertaEm: '2026-07-18T09:00:00-03:00',
  saldoAberturaCentavos: 20000,
  totalEntradasCentavos: 10000,
  totalSaidasCentavos: 3000,
  saldoEsperadoCentavos: 27000,
  fechadaEm: null,
  saldoInformadoCentavos: null,
  diferencaCentavos: null,
  movimentos: [],
};

function renderComToast() {
  return render(
    <ToastProvider>
      <FluxoCaixa />
    </ToastProvider>,
  );
}

describe('Financeiro › Fluxo de Caixa (smoke)', () => {
  beforeEach(() => {
    caixaAtual.mockReset().mockResolvedValue(SESSAO_ABERTA);
    caixaHistorico.mockReset().mockResolvedValue([]);
    contasBancarias.mockReset().mockResolvedValue(CONTAS);
  });

  afterEach(() => cleanup());

  it('carrega o board (sessão aberta real) e monta a tela sem lançar', async () => {
    renderComToast();

    await waitFor(() => expect(screen.getAllByText('João').length).toBeGreaterThan(0));
    expect(caixaAtual).toHaveBeenCalled();
  });

  it('sem sessão aberta e sem histórico: estado vazio — não trava nem quebra', async () => {
    caixaAtual.mockReset().mockResolvedValue(null);

    renderComToast();

    await waitFor(() => expect(caixaAtual).toHaveBeenCalled());
    expect(screen.queryByText('Não deu para carregar')).not.toBeInTheDocument();
  });

  it('erro ao carregar mostra estado de erro — não trava em skeleton', async () => {
    caixaAtual.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));
    caixaHistorico.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    renderComToast();

    await waitFor(() => expect(screen.getByText('Não deu para carregar o caixa')).toBeInTheDocument());
  });
});
