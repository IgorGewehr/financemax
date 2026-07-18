import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';

import { AppShell } from '@/components/layout/AppShell';
import { FinanceiroLayout } from '@/pages/financeiro/FinanceiroLayout';
import { VisaoGeral } from '@/pages/financeiro/VisaoGeral';

/**
 * As 8 sub-telas além da `VisaoGeral` (a home/index, mantida eager pra 1º paint sem flash) são
 * `lazy` — cada uma carrega recharts/framer-motion-heavy só quando a aba é aberta. Reduz o chunk
 * inicial (era 666kB/193kB gzip, acima do limite de aviso do Vite) sem tocar em nenhuma tela.
 */
const EntradasSaidas = lazy(() =>
  import('@/pages/financeiro/EntradasSaidas').then((m) => ({ default: m.EntradasSaidas })),
);
const Recorrentes = lazy(() =>
  import('@/pages/financeiro/Recorrentes').then((m) => ({ default: m.Recorrentes })),
);
const Projetos = lazy(() =>
  import('@/pages/financeiro/Projetos').then((m) => ({ default: m.Projetos })),
);
const Bancario = lazy(() =>
  import('@/pages/financeiro/Bancario').then((m) => ({ default: m.Bancario })),
);
const FluxoCaixa = lazy(() =>
  import('@/pages/financeiro/FluxoCaixa').then((m) => ({ default: m.FluxoCaixa })),
);
const RoiNegocio = lazy(() =>
  import('@/pages/financeiro/RoiNegocio').then((m) => ({ default: m.RoiNegocio })),
);
const Relatorios = lazy(() =>
  import('@/pages/financeiro/Relatorios').then((m) => ({ default: m.Relatorios })),
);
const Configuracoes = lazy(() =>
  import('@/pages/financeiro/Configuracoes').then((m) => ({ default: m.Configuracoes })),
);

function RouteFallback() {
  return (
    <div className="flex h-40 items-center justify-center">
      <div className="h-6 w-6 animate-spin rounded-full border-2 border-border/70 border-t-primary-600" />
    </div>
  );
}

/**
 * financemax/web é financeiro-only (§1 do prompt da F3): não existe Dashboard/PDV/Vendas/Estoque/
 * Compras/Ordens/Clientes/Agenda aqui — o app abre DIRETO no Financeiro. Sem `Sidebar` (ver
 * `AppShell`): a navegação entre as 9 sub-telas é só a barra de abas do `FinanceiroLayout`. As
 * rotas continuam prefixadas em `/financeiro/*` de propósito — é o path que `FinanceiroLayout`
 * (copiado 1:1 do sistemax) já usa nos `NavLink`, então zero look-alike-mas-quebrado.
 */
export function App() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/financeiro" replace />} />
        <Route path="financeiro" element={<FinanceiroLayout />}>
          <Route index element={<VisaoGeral />} />
          <Route
            path="entradas-saidas"
            element={
              <Suspense fallback={<RouteFallback />}>
                <EntradasSaidas />
              </Suspense>
            }
          />
          <Route
            path="recorrentes"
            element={
              <Suspense fallback={<RouteFallback />}>
                <Recorrentes />
              </Suspense>
            }
          />
          <Route
            path="projetos"
            element={
              <Suspense fallback={<RouteFallback />}>
                <Projetos />
              </Suspense>
            }
          />
          <Route
            path="bancario"
            element={
              <Suspense fallback={<RouteFallback />}>
                <Bancario />
              </Suspense>
            }
          />
          <Route
            path="fluxo-de-caixa"
            element={
              <Suspense fallback={<RouteFallback />}>
                <FluxoCaixa />
              </Suspense>
            }
          />
          <Route
            path="roi-negocio"
            element={
              <Suspense fallback={<RouteFallback />}>
                <RoiNegocio />
              </Suspense>
            }
          />
          <Route
            path="relatorios"
            element={
              <Suspense fallback={<RouteFallback />}>
                <Relatorios />
              </Suspense>
            }
          />
          <Route
            path="configuracoes"
            element={
              <Suspense fallback={<RouteFallback />}>
                <Configuracoes />
              </Suspense>
            }
          />
        </Route>
        <Route path="*" element={<Navigate to="/financeiro" replace />} />
      </Route>
    </Routes>
  );
}
