import { Navigate, Route, Routes } from 'react-router-dom';

import { AppShell } from '@/components/layout/AppShell';
import { Bancario } from '@/pages/financeiro/Bancario';
import { Configuracoes } from '@/pages/financeiro/Configuracoes';
import { EntradasSaidas } from '@/pages/financeiro/EntradasSaidas';
import { FinanceiroLayout } from '@/pages/financeiro/FinanceiroLayout';
import { FluxoCaixa } from '@/pages/financeiro/FluxoCaixa';
import { Projetos } from '@/pages/financeiro/Projetos';
import { Recorrentes } from '@/pages/financeiro/Recorrentes';
import { Relatorios } from '@/pages/financeiro/Relatorios';
import { RoiNegocio } from '@/pages/financeiro/RoiNegocio';
import { VisaoGeral } from '@/pages/financeiro/VisaoGeral';

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
          <Route path="entradas-saidas" element={<EntradasSaidas />} />
          <Route path="recorrentes" element={<Recorrentes />} />
          <Route path="projetos" element={<Projetos />} />
          <Route path="bancario" element={<Bancario />} />
          <Route path="fluxo-de-caixa" element={<FluxoCaixa />} />
          <Route path="roi-negocio" element={<RoiNegocio />} />
          <Route path="relatorios" element={<Relatorios />} />
          <Route path="configuracoes" element={<Configuracoes />} />
        </Route>
        <Route path="*" element={<Navigate to="/financeiro" replace />} />
      </Route>
    </Routes>
  );
}
