// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ContaBancariaDto, DreDto, ExtratoDto, ExtratoLinhaDto, FluxoDeCaixaDto } from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { EntradasSaidas } from './EntradasSaidas';

const extrato = vi.fn();
const relatoriosDre = vi.fn();
const fluxo = vi.fn();
const contasBancarias = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    extrato: (...args: unknown[]) => extrato(...args),
    relatoriosDre: (...args: unknown[]) => relatoriosDre(...args),
    fluxo: (...args: unknown[]) => fluxo(...args),
    contasBancarias: (...args: unknown[]) => contasBancarias(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const LINHA: ExtratoLinhaDto = {
  id: 'l1',
  data: '2026-07-10',
  descricao: 'Fornecedor Insumos SA',
  categoriaId: 'cmv-fornecedor',
  tipo: 'saida',
  status: 'pago',
  valor: money(120000),
  conta: 'Itaú',
  origem: 'Baixa manual',
  diasAtraso: null,
};

const EXTRATO_MES: ExtratoDto = { linhas: [LINHA], kpis: { totalEntradas: money(0), totalSaidas: money(120000), saldoPeriodo: money(-120000) } };
const EXTRATO_HORIZONTE: ExtratoDto = { linhas: [LINHA], kpis: { totalEntradas: money(0), totalSaidas: money(120000), saldoPeriodo: money(-120000) } };

const DRE: DreDto = { receitaBruta: money(0), custoDireto: money(0), despesaOperacional: money(120000), resultadoOperacional: money(-120000), porCorrente: [] };

const FLUXO: FluxoDeCaixaDto = { pontos: [], primeiroDiaNegativo: null };

const CONTAS: ContaBancariaDto[] = [{ id: 'c1', nome: 'Itaú', tipo: 'corrente', saldo: money(500000), ativa: true }];

function renderComToast() {
  return render(
    <ToastProvider>
      <EntradasSaidas />
    </ToastProvider>,
  );
}

describe('Financeiro › Entradas & saídas (smoke)', () => {
  beforeEach(() => {
    extrato.mockReset().mockImplementation((de: string) => Promise.resolve(de === '2015-01-01' ? EXTRATO_HORIZONTE : EXTRATO_MES));
    relatoriosDre.mockReset().mockResolvedValue(DRE);
    fluxo.mockReset().mockResolvedValue(FLUXO);
    contasBancarias.mockReset().mockResolvedValue(CONTAS);
  });

  afterEach(() => cleanup());

  it('carrega timeline + KPIs + Super Consultor de Fornecedores com dado real (não trava em skeleton)', async () => {
    renderComToast();

    await waitFor(() => expect(screen.getAllByText('Fornecedor Insumos SA').length).toBeGreaterThan(0));
    expect(extrato).toHaveBeenCalled();
    expect(relatoriosDre).toHaveBeenCalled();
    expect(fluxo).toHaveBeenCalled();
  });

  it('sem lançamentos no período: KPIs e timeline renderizam zerados — não travam nem quebram', async () => {
    extrato.mockReset().mockResolvedValue({ linhas: [], kpis: { totalEntradas: money(0), totalSaidas: money(0), saldoPeriodo: money(0) } });
    relatoriosDre.mockReset().mockResolvedValue({ receitaBruta: money(0), custoDireto: money(0), despesaOperacional: money(0), resultadoOperacional: money(0), porCorrente: [] });

    renderComToast();

    await waitFor(() => expect(screen.getByText('0 lançamentos · 0 atrasados')).toBeInTheDocument());
  });

  it('erro ao carregar o extrato do mês mostra estado de erro na linha do tempo — não finge lista vazia', async () => {
    extrato.mockReset().mockImplementation((de: string) =>
      de === '2015-01-01' ? Promise.resolve(EXTRATO_HORIZONTE) : Promise.reject(new ApiError('erro_interno', 'Serviço fora do ar', 500)),
    );

    renderComToast();

    await waitFor(() => expect(screen.getByText('Não deu para carregar a linha do tempo')).toBeInTheDocument());
  });
});
