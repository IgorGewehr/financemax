import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import {
  ApiError,
  login as loginRequest,
  logout as logoutSession,
  readSession,
  setUnauthorizedHandler,
  type Session,
} from './api/client';

interface AuthContextValue {
  session: Session | null;
  login: (email: string, senha: string) => Promise<void>;
  logout: () => void;
  loading: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Fonte da verdade de sessão da SPA — espelha o papel do `AuthProvider` do saas-erp (mesma
 * convenção CLAUDE.md). Sessão é o par access+refresh JWT emitido por `POST /api/auth/login`
 * (e-mail+senha, ver `lib/api/client.ts`), não mais boot-token+PIN local do sistemax de origem.
 * `setUnauthorizedHandler` (client.ts) devolve qualquer 401 que sobreviva a uma tentativa de
 * refresh pra este estado, sem cada tela precisar tratar token expirado individualmente.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => readSession());
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setUnauthorizedHandler(() => setSession(null));
    return () => setUnauthorizedHandler(null);
  }, []);

  const login = useCallback(async (email: string, senha: string) => {
    setLoading(true);
    try {
      const novaSessao = await loginRequest(email, senha);
      setSession(novaSessao);
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    logoutSession();
    setSession(null);
  }, []);

  const value = useMemo<AuthContextValue>(() => ({ session, login, logout, loading }), [session, login, logout, loading]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>');
  return ctx;
}

export { ApiError };
