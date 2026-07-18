// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AuthGate } from './AuthGate';

const useAuthMock = vi.fn();

vi.mock('@/lib/auth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('@/pages/Login', () => ({
  Login: () => <div>Tela de Login (mock)</div>,
}));

/** Cobre a lógica de roteamento do porteiro isoladamente (Login mockado) — sem PIN local nem
 * wizard de 1º-boot (removidos na F3, ver `lib/auth.tsx`): só sessão / sem sessão. */
describe('AuthGate', () => {
  afterEach(cleanup);

  it('sem sessão: mostra Login e não monta nenhuma rota do app', () => {
    useAuthMock.mockReturnValue({ session: null });

    render(
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>,
    );

    expect(screen.getByText('Tela de Login (mock)')).toBeInTheDocument();
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();
  });

  it('com sessão: libera as rotas reais do app', () => {
    useAuthMock.mockReturnValue({ session: { accessToken: 'a', refreshToken: 'b', expiraEm: '', usuario: { id: '1', nome: 'Igor', email: 'igor@financemax.app', papel: 'founder', ativo: true, mustChangePassword: false } } });

    render(
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>,
    );

    expect(screen.getByText('Rota real do App')).toBeInTheDocument();
    expect(screen.queryByText('Tela de Login (mock)')).not.toBeInTheDocument();
  });
});
