import type { ReactNode } from 'react';

import { useAuth } from '@/lib/auth';
import { Login } from '@/pages/Login';

/** Porteiro da SPA — sem sessão válida (par access+refresh e-mail/senha, ver `lib/auth.tsx`), só a
 * tela de Login existe: nenhuma rota do `App` monta, nenhuma chamada de API dispara sem token. */
export function AuthGate({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  if (!session) return <Login />;
  return <>{children}</>;
}
