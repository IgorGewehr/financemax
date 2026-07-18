import { Outlet, useLocation } from 'react-router-dom';

import { ErrorBoundary } from './ErrorBoundary';
import { TopBar } from './TopBar';

/**
 * Casca do app financeiro-only — sem `Sidebar` (não há outro módulo pra navegar pra fora do
 * Financeiro). `TopBar` fica sozinha no topo; a navegação real entre as 9 sub-telas é a barra de
 * abas do `FinanceiroLayout`, logo abaixo. `ErrorBoundary` com `key={pathname}` isola um crash de
 * render numa sub-tela (ex.: dado real fora do formato que o TS promete) sem branquear o app
 * inteiro — trocar de aba remonta a fronteira.
 */
export function AppShell() {
  const { pathname } = useLocation();
  return (
    <div className="flex h-dvh w-full flex-col overflow-hidden bg-background text-foreground">
      <TopBar />
      <main className="flex-1 overflow-y-auto">
        <ErrorBoundary key={pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
