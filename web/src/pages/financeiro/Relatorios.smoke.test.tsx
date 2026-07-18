// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ContasEmAbertoDto, DreDto, ReceitaRecorrenteDto } from '@/lib/api/financeiro';

import { Relatorios } from './Relatorios';

const receitaRecorrente = vi.fn();
const relatoriosContasEmAberto = vi.fn();
const relatoriosDre = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    receitaRecorrente: (...args: unknown[]) => receitaRecorrente(...args),
    relatoriosContasEmAberto: (...args: unknown[]) => relatoriosContasEmAberto(...args),
    relatoriosDre: (...args: unknown[]) => relatoriosDre(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const RECEITA_RECORRENTE: ReceitaRecorrenteDto = {
  mrr: money(300000),
  arr: money(3600000),
  assinaturasAtivas: 12,
  ticketMedio: money(25000),
  mrrNovoNoMes: money(10000),
  mrrChurnNoMes: money(5000),
  clientesChurnNoMes: 1,
  churnPercent: 2,
  porServico: [],
  maiorConcentracao: null,
};

const CONTAS_EM_ABERTO: ContasEmAbertoDto = {
  receberEmAberto: money(100000),
  receberAtrasado: money(20000),
  pagarEmAberto: money(80000),
  agingBuckets: [{ id: '0-15', label: '0–15 dias', valor: money(50000) }],
};

const DRE: DreDto = { receitaBruta: money(200000), custoDireto: money(50000), despesaOperacional: money(30000), resultadoOperacional: money(120000), porCorrente: [] };

describe('Financeiro › Relatórios (smoke)', () => {
  beforeEach(() => {
    receitaRecorrente.mockReset().mockResolvedValue(RECEITA_RECORRENTE);
    relatoriosContasEmAberto.mockReset().mockResolvedValue(CONTAS_EM_ABERTO);
    relatoriosDre.mockReset().mockResolvedValue(DRE);
  });

  afterEach(() => cleanup());

  it('carrega MRR + contas em aberto + DRE e monta a tela sem lançar', async () => {
    render(<Relatorios />);

    await waitFor(() => expect(relatoriosDre).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryByText(/Não deu para carregar/)).not.toBeInTheDocument());
  });

  it('erro num bloco mostra a faixa de erro dedicada — não trava em skeleton', async () => {
    relatoriosDre.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    render(<Relatorios />);

    await waitFor(() => expect(screen.getByText(/Não deu para carregar/)).toBeInTheDocument());
  });
});
