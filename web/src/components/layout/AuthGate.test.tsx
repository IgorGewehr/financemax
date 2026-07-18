// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AuthGate } from './AuthGate';

const useAuthMock = vi.fn();

vi.mock('@/lib/auth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('@/pages/Login', () => ({
  Login: () => <div>Tela de Login (mock)</div>,
}));

vi.mock('@/pages/CriarConta', () => ({
  CriarConta: () => <div>Tela de Criar Conta (mock)</div>,
}));

function renderEm(pathname: string) {
  return render(
    <MemoryRouter initialEntries={[pathname]}>
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>
    </MemoryRouter>,
  );
}

/** Cobre a lógica de roteamento do porteiro isoladamente (Login/CriarConta mockados) — sem PIN
 * local nem wizard de 1º-boot (removidos na F3, ver `lib/auth.tsx`): sessão / sem sessão / rota de
 * onboarding (exceção que abre com OU sem sessão). */
describe('AuthGate', () => {
  afterEach(cleanup);

  it('sem sessão: mostra Login e não monta nenhuma rota do app', () => {
    useAuthMock.mockReturnValue({ session: null });

    renderEm('/financeiro');

    expect(screen.getByText('Tela de Login (mock)')).toBeInTheDocument();
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();
  });

  it('com sessão: libera as rotas reais do app', () => {
    useAuthMock.mockReturnValue({ session: { accessToken: 'a', refreshToken: 'b', expiraEm: '', usuario: { id: '1', nome: 'Igor', email: 'igor@financemax.app', papel: 'founder', ativo: true, mustChangePassword: false } } });

    renderEm('/financeiro');

    expect(screen.getByText('Rota real do App')).toBeInTheDocument();
    expect(screen.queryByText('Tela de Login (mock)')).not.toBeInTheDocument();
  });

  it('/criar-conta sem sessão: mostra a tela de onboarding, não o Login', () => {
    useAuthMock.mockReturnValue({ session: null });

    renderEm('/criar-conta?token=abc123');

    expect(screen.getByText('Tela de Criar Conta (mock)')).toBeInTheDocument();
    expect(screen.queryByText('Tela de Login (mock)')).not.toBeInTheDocument();
  });

  it('/criar-conta com sessão: ainda mostra a tela de onboarding (link de convite gerado pelo próprio founder)', () => {
    useAuthMock.mockReturnValue({ session: { accessToken: 'a', refreshToken: 'b', expiraEm: '', usuario: { id: '1', nome: 'Igor', email: 'igor@financemax.app', papel: 'founder', ativo: true, mustChangePassword: false } } });

    renderEm('/criar-conta?token=abc123');

    expect(screen.getByText('Tela de Criar Conta (mock)')).toBeInTheDocument();
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();
  });
});
