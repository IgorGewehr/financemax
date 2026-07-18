import { Outlet } from 'react-router-dom';

import { TopBar } from './TopBar';

/**
 * Casca do app financeiro-only — sem `Sidebar` (não há outro módulo pra navegar pra fora do
 * Financeiro). `TopBar` fica sozinha no topo; a navegação real entre as 9 sub-telas é a barra de
 * abas do `FinanceiroLayout`, logo abaixo.
 */
export function AppShell() {
  return (
    <div className="flex h-dvh w-full flex-col overflow-hidden bg-background text-foreground">
      <TopBar />
      <main className="flex-1 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}
