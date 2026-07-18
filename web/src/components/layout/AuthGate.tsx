import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';

import { useAuth } from '@/lib/auth';
import { CriarConta } from '@/pages/CriarConta';
import { Login } from '@/pages/Login';

/** Porteiro da SPA — sem sessão válida (par access+refresh e-mail/senha, ver `lib/auth.tsx`), só a
 * tela de Login existe: nenhuma rota do `App` monta, nenhuma chamada de API dispara sem token.
 * `/criar-conta` é a ÚNICA exceção: precisa abrir tanto sem sessão (1º dono / aceitar convite,
 * usuário ainda não existe) quanto com sessão (alguém logado clicando no próprio link de convite
 * gerado em Configurações) — checada ANTES do gate de sessão. */
export function AuthGate({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  const { pathname } = useLocation();
  if (pathname === '/criar-conta') return <CriarConta />;
  if (!session) return <Login />;
  return <>{children}</>;
}
