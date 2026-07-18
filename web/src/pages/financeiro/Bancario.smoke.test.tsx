// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type {
  ConciliacaoBancariaDto,
  ContaBancariaDto,
  MovimentoBancarioDto,
  SemanaMovimentoDto,
  TaxasPorFormaDto,
} from '@/lib/api/financeiro';

import { Bancario } from './Bancario';

const contasBancarias = vi.fn();
const movimentos = vi.fn();
const movimentosSemana = vi.fn();
const conciliacao = vi.fn();
const taxasPorForma = vi.fn();
const confirmarConciliacao = vi.fn();
const ignorarConciliacao = vi.fn();
const simularEmprestimo = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    contasBancarias: (...args: unknown[]) => contasBancarias(...args),
    movimentos: (...args: unknown[]) => movimentos(...args),
    movimentosSemana: (...args: unknown[]) => movimentosSemana(...args),
    conciliacao: (...args: unknown[]) => conciliacao(...args),
    taxasPorForma: (...args: unknown[]) => taxasPorForma(...args),
    confirmarConciliacao: (...args: unknown[]) => confirmarConciliacao(...args),
    ignorarConciliacao: (...args: unknown[]) => ignorarConciliacao(...args),
    simularEmprestimo: (...args: unknown[]) => simularEmprestimo(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const CONTAS: ContaBancariaDto[] = [{ id: 'c1', nome: 'Itaú', tipo: 'corrente', saldo: money(500000), ativa: true }];

const SEMANAS: SemanaMovimentoDto[] = [
  {
    numero: 1,
    inicio: '2026-07-01',
    fim: '2026-07-07',
    parcial: false,
    dias: [{ dia: '2026-07-01', entradas: money(10000), saidas: money(5000) }],
  },
];

const MOVIMENTOS: MovimentoBancarioDto[] = [
  { id: 'm1', data: '2026-07-01', descricao: 'Venda balcão', forma: 'pix', contaBancariaCaixaId: 'c1', valor: money(10000), conciliado: true },
];

const CONCILIACAO: ConciliacaoBancariaDto = {
  bateuCertinhoTotal: 1,
  bateuCertinhoAmostra: [],
  sobrouNoBanco: [],
  sobrouNoSistema: [],
};

const TAXAS: TaxasPorFormaDto = {
  taxaTotal: money(500),
  volumeTotal: money(10000),
  percentualVolume: 5,
  porForma: [{ formaPagamentoId: 'pix', forma: 'PIX', volume: money(10000), taxaPercentual: 0.005, taxa: money(500) }],
};

describe('Financeiro › Bancário (smoke)', () => {
  beforeEach(() => {
    contasBancarias.mockReset().mockResolvedValue(CONTAS);
    movimentos.mockReset().mockResolvedValue(MOVIMENTOS);
    movimentosSemana.mockReset().mockResolvedValue(SEMANAS);
    conciliacao.mockReset().mockResolvedValue(CONCILIACAO);
    taxasPorForma.mockReset().mockResolvedValue(TAXAS);
    confirmarConciliacao.mockReset().mockResolvedValue(undefined);
    ignorarConciliacao.mockReset().mockResolvedValue(undefined);
    simularEmprestimo.mockReset();
  });

  afterEach(() => cleanup());

  it('carrega os 5 blocos de dado real e monta a tela sem travar em skeleton', async () => {
    render(<Bancario />);

    // Conta real (não fabricada) aparece no filtro do extrato e na legenda da tabela.
    await waitFor(() => expect(screen.getAllByText('Itaú').length).toBeGreaterThan(0));
    // Movimento real aparece na tabela de extrato.
    expect(screen.getByText('Venda balcão')).toBeInTheDocument();
    // Simulador de empréstimo (Bancário) é composto na mesma página.
    expect(screen.getByText('Simulação de empréstimo')).toBeInTheDocument();
    expect(contasBancarias).toHaveBeenCalled();
    expect(taxasPorForma).toHaveBeenCalled();
  });

  it('erro ao carregar contas/extrato mostra estado de erro — não trava em skeleton nem finge dado vazio', async () => {
    contasBancarias.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));
    movimentos.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    render(<Bancario />);

    await waitFor(() => expect(screen.getAllByText('Não deu para carregar').length).toBeGreaterThan(0));
  });
});
