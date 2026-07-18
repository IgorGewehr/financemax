// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { ConfiguracaoFinanceiraDto } from '@/lib/api/financeiro';
import { AuthProvider } from '@/lib/auth';
import { ToastProvider } from '@/lib/toast';

import { Configuracoes } from './Configuracoes';

const configuracoes = vi.fn();
const salvarConfiguracoes = vi.fn();
const criarConvite = vi.fn();
const listarConvites = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    salvarConfiguracoes: (...args: unknown[]) => salvarConfiguracoes(...args),
  },
}));

vi.mock('@/lib/api/onboarding', () => ({
  onboardingApi: {
    criarConvite: (...args: unknown[]) => criarConvite(...args),
    listarConvites: (...args: unknown[]) => listarConvites(...args),
  },
}));

const CONFIG: ConfiguracaoFinanceiraDto = {
  analisePorProjetoAtiva: true,
  custoHoraPadraoCentavos: null,
  tempoEntraNoDre: false,
  imobilizadoRoiAtivo: false,
  taxaDescontoAnualBps: null,
  inicioOperacao: null,
};

const SESSION_FOUNDER = {
  accessToken: 'a',
  refreshToken: 'b',
  expiraEm: '2099-01-01T00:00:00Z',
  usuario: { id: 'u1', nome: 'Dona Fundadora', email: 'dona@empresa.com', papel: 'founder', ativo: true, mustChangePassword: false },
};

function renderComProviders() {
  return render(
    <ToastProvider>
      <AuthProvider>
        <Configuracoes />
      </AuthProvider>
    </ToastProvider>,
  );
}

describe('Financeiro › Configurações (smoke)', () => {
  beforeEach(() => {
    localStorage.clear();
    configuracoes.mockReset().mockResolvedValue(CONFIG);
    salvarConfiguracoes.mockReset().mockResolvedValue(CONFIG);
    criarConvite.mockReset().mockResolvedValue({ token: 'tok123', email: 'novo@empresa.com', papel: 'operator', expiraEm: '2026-07-25T00:00:00Z' });
    listarConvites.mockReset().mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it('sem sessão: carrega os toggles e monta a tela sem lançar (Convidar fica oculto)', async () => {
    renderComProviders();

    await waitFor(() => expect(screen.getByText('Análise por Projeto')).toBeInTheDocument());
    expect(configuracoes).toHaveBeenCalled();
    expect(screen.queryByText('Convidar')).not.toBeInTheDocument();
  });

  it('founder logado: mostra "Convidar" e monta a seção de convites sem lançar', async () => {
    localStorage.setItem('financemax:session', JSON.stringify(SESSION_FOUNDER));

    renderComProviders();

    await waitFor(() => expect(screen.getByText('Convidar')).toBeInTheDocument());
    expect(listarConvites).toHaveBeenCalled();
  });

  it('erro ao carregar mostra estado de erro — não trava em skeleton', async () => {
    configuracoes.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    renderComProviders();

    await waitFor(() => expect(screen.getByText('Não deu para carregar')).toBeInTheDocument());
  });
});
