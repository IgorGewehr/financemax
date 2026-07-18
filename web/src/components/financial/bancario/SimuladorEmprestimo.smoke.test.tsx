// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { SimulacaoEmprestimoDto } from '@/lib/api/financeiro';

import { SimuladorEmprestimo } from './SimuladorEmprestimo';

const simularEmprestimo = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    simularEmprestimo: (...args: unknown[]) => simularEmprestimo(...args),
  },
}));

const RESULTADO: SimulacaoEmprestimoDto = {
  parcelaMensalCentavos: 187123,
  custoTotalCentavos: 2245476,
  jurosTotaisCentavos: 245476,
  taxaEfetivaAnualPercent: 12.68,
  paybackMeses: 8,
  veredito: 'viavel',
  motivo: 'A parcela cabe confortavelmente no fluxo de caixa atual.',
};

function preencherFormulario() {
  fireEvent.change(screen.getByLabelText('Valor do empréstimo (R$)'), { target: { value: '20000' } });
  fireEvent.change(screen.getByLabelText('Juros ao mês (%)'), { target: { value: '1,5'.replace(',', '.') } });
  fireEvent.change(screen.getByLabelText('Prazo (meses)'), { target: { value: '12' } });
}

describe('Bancário › Simulador de empréstimo (smoke)', () => {
  beforeEach(() => {
    simularEmprestimo.mockReset();
  });

  afterEach(() => cleanup());

  it('estado inicial não mostra resultado nem chama a API', () => {
    render(<SimuladorEmprestimo />);

    expect(screen.getByText(/Preencha os termos e clique em "Simular"/)).toBeInTheDocument();
    expect(simularEmprestimo).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Simular' })).toBeDisabled();
  });

  it('preenche, envia e renderiza o resultado real vindo do servidor (Tabela Price)', async () => {
    simularEmprestimo.mockResolvedValue(RESULTADO);
    render(<SimuladorEmprestimo />);

    preencherFormulario();
    expect(screen.getByRole('button', { name: 'Simular' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: 'Simular' }));

    expect(simularEmprestimo).toHaveBeenCalledWith({
      valorCentavos: 2000000,
      taxaJurosMensalBps: 150,
      prazoMeses: 12,
      retornoMensalEsperadoCentavos: null,
    });

    await waitFor(() => expect(screen.getByText('Viável')).toBeInTheDocument());

    expect(screen.getByText(RESULTADO.motivo)).toBeInTheDocument();
    expect(screen.getByText('R$ 1.871')).toBeInTheDocument(); // parcela mensal (arredondada, sem centavos)
    expect(screen.getByText('12,68%')).toBeInTheDocument(); // taxa efetiva anual
    expect(screen.getByText('8 meses')).toBeInTheDocument(); // payback do equipamento (só aparece quando informado)
  });

  it('envia o retorno mensal esperado quando informado — habilita payback', async () => {
    simularEmprestimo.mockResolvedValue(RESULTADO);
    render(<SimuladorEmprestimo />);

    preencherFormulario();
    fireEvent.change(screen.getByLabelText('Retorno mensal do equip. (R$)'), { target: { value: '3000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Simular' }));

    expect(simularEmprestimo).toHaveBeenCalledWith(expect.objectContaining({ retornoMensalEsperadoCentavos: 300000 }));
    await waitFor(() => expect(screen.getByText('Viável')).toBeInTheDocument());
  });

  it('erro do servidor mostra mensagem — não trava em "Calculando…" nem finge sucesso', async () => {
    simularEmprestimo.mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));
    render(<SimuladorEmprestimo />);

    preencherFormulario();
    fireEvent.click(screen.getByRole('button', { name: 'Simular' }));

    await waitFor(() => expect(screen.getByText('Não deu para simular: Serviço fora do ar')).toBeInTheDocument());
    expect(screen.queryByText('Viável')).not.toBeInTheDocument();
  });
});
