// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type {
  ConfiguracaoFinanceiraDto,
  ContasEmAbertoDto,
  DisponivelParaRetiradaDto,
  DreDto,
  ExtratoDto,
  FatoReceitaDiariaDto,
  FluxoDeCaixaDto,
  PrevisaoDeCaixaDto,
  RadarDoSimplesDto,
  ReceitaRecorrenteDto,
} from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { VisaoGeral } from './VisaoGeral';

const previsaoCaixa = vi.fn();
const disponivelParaRetirada = vi.fn();
const fluxo = vi.fn();
const relatoriosContasEmAberto = vi.fn();
const extrato = vi.fn();
const relatoriosDre = vi.fn();
const fatoReceitaDiaria = vi.fn();
const receitaRecorrente = vi.fn();
const configuracoes = vi.fn();
const roiNegocio = vi.fn();
const radarSimples = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    previsaoCaixa: (...args: unknown[]) => previsaoCaixa(...args),
    disponivelParaRetirada: (...args: unknown[]) => disponivelParaRetirada(...args),
    fluxo: (...args: unknown[]) => fluxo(...args),
    relatoriosContasEmAberto: (...args: unknown[]) => relatoriosContasEmAberto(...args),
    extrato: (...args: unknown[]) => extrato(...args),
    relatoriosDre: (...args: unknown[]) => relatoriosDre(...args),
    fatoReceitaDiaria: (...args: unknown[]) => fatoReceitaDiaria(...args),
    receitaRecorrente: (...args: unknown[]) => receitaRecorrente(...args),
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    roiNegocio: (...args: unknown[]) => roiNegocio(...args),
    radarSimples: (...args: unknown[]) => radarSimples(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const PREVISAO: PrevisaoDeCaixaDto = {
  bandas: [],
  probabilidadeSaldoNegativoEm30Dias: 0.1,
  primeiroDiaP50Negativo: null,
  diasRunwayBruto: 45,
  diasRunwayRealista: 40,
};

const DISPONIVEL: DisponivelParaRetiradaDto = { saldoEmCaixa: money(500000), jaTemDono: money(100000), podeTirar: money(400000) };

const FLUXO: FluxoDeCaixaDto = {
  pontos: [
    { data: '2026-07-10', entradas: money(10000), saidas: money(5000), saldoAcumulado: money(50000), projetado: false },
    { data: '2026-07-11', entradas: money(0), saidas: money(0), saldoAcumulado: money(50000), projetado: true },
  ],
  primeiroDiaNegativo: null,
};

const CONTAS_EM_ABERTO: ContasEmAbertoDto = {
  receberEmAberto: money(100000),
  receberAtrasado: money(20000),
  pagarEmAberto: money(80000),
  agingBuckets: [],
};

const EXTRATO_SAIDA: ExtratoDto = { linhas: [], kpis: { totalEntradas: money(0), totalSaidas: money(0), saldoPeriodo: money(0) } };

const DRE: DreDto = { receitaBruta: money(200000), custoDireto: money(50000), despesaOperacional: money(30000), resultadoOperacional: money(120000), porCorrente: [] };

const FATOS_RECEITA: FatoReceitaDiariaDto[] = [
  { tenantId: 't1', dia: '2026-07-01', receitaCentavos: 10000, atualizadoEmUtc: '2026-07-01T00:00:00Z' },
  { tenantId: 't1', dia: '2026-07-02', receitaCentavos: 12000, atualizadoEmUtc: '2026-07-02T00:00:00Z' },
];

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

const RADAR: RadarDoSimplesDto = {
  rbt12Centavos: 1000000,
  faixaAtual: 1,
  aliquotaEfetiva: 0.06,
  aliquotaNominalFaixaAtual: 0.06,
  distanciaAoProximoDegrauCentavos: 500000,
  mesesProjetadosAteOProximoDegrau: 6,
};

const CONFIG_OFF: ConfiguracaoFinanceiraDto = {
  analisePorProjetoAtiva: false,
  custoHoraPadraoCentavos: null,
  tempoEntraNoDre: false,
  imobilizadoRoiAtivo: false,
  taxaDescontoAnualBps: null,
  inicioOperacao: null,
};

function renderComProviders() {
  return render(
    <MemoryRouter>
      <ToastProvider>
        <VisaoGeral />
      </ToastProvider>
    </MemoryRouter>,
  );
}

describe('Financeiro › Visão Geral (smoke)', () => {
  beforeEach(() => {
    previsaoCaixa.mockReset().mockResolvedValue(PREVISAO);
    disponivelParaRetirada.mockReset().mockResolvedValue(DISPONIVEL);
    fluxo.mockReset().mockResolvedValue(FLUXO);
    relatoriosContasEmAberto.mockReset().mockResolvedValue(CONTAS_EM_ABERTO);
    extrato.mockReset().mockResolvedValue(EXTRATO_SAIDA);
    relatoriosDre.mockReset().mockResolvedValue(DRE);
    fatoReceitaDiaria.mockReset().mockResolvedValue(FATOS_RECEITA);
    receitaRecorrente.mockReset().mockResolvedValue(RECEITA_RECORRENTE);
    configuracoes.mockReset().mockResolvedValue(CONFIG_OFF);
    roiNegocio.mockReset().mockRejectedValue(new ApiError('nao_encontrado', 'Painel desligado', 404));
    radarSimples.mockReset().mockResolvedValue(RADAR);
  });

  it('carrega os blocos e monta a tela sem lançar', async () => {
    renderComProviders();

    await waitFor(() => expect(previsaoCaixa).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryAllByText(/carregar/i).length).toBe(0));
  });

  it('erro num bloco mostra estado de erro isolado — não derruba a tela inteira', async () => {
    previsaoCaixa.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    renderComProviders();

    await waitFor(() => expect(screen.getAllByText('Não deu para carregar').length).toBeGreaterThan(0));
  });
});
