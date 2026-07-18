# financemax — escopo do MVP (confirmado com o dono, 2026-07-18)

App **financeiro-only** que extrai a "máquina financeira" do SistemaX. Multiplataforma, mas o MVP é **desktop Mac + Windows**.

## Decisões do dono
- **Plataformas:** só **Mac + Windows** por enquanto (Tauri 2 + WebView2/WebKit). Mobile (Capacitor) fica pra depois.
- **Sem sidebar:** só o Financeiro. As abas do módulo direto (Visão geral, Entradas & saídas, Recorrentes, Projetos, Bancário, Fluxo de caixa, Investimento & ROI, Relatórios, Configurações).
- **Visão Geral = v3** (visual-first: gauge de fôlego, gráfico de projeção, tiles/sparkline, rosca do mix, barras — sem Super Consultor na tela, sem título). Contrato: `sistemax/docs/ui/mockups/visao-geral-v3.html`.
- **Multi-usuário ONLINE:** o dono (Mac) + colega (Windows), **mesma base** (mesmo negócio/financeiro). Login **e-mail + senha** robusto (JWT). Acesso remoto via **Cloudflare Tunnel** (URL pública segura, sem abrir porta).
- **Entrada de dados MANUAL** por enquanto — **sem gateway DigiSat** nesta fase (vem depois).
- **IA:** Super Consultor com **OpenAI** (chave = a mesma do `saas-erp/.env`, já copiada pro `financemax/.env` gitignored), modelo barato, com fallback determinístico (template) pra não gastar à toa.
- **Servidor:** monólito modular .NET 10 headless (`Financemax.Api`), **dockerizado**, **SQLite (WAL)** pro MVP (eficiente p/ poucos usuários, deploy trivial); **Postgres** = upgrade documentado quando virar equipe.

## Fases
- **F1 (em andamento):** extrair o módulo Financeiro pra `server/` self-contained (Financemax.Api + SQLite + Dockerfile + docker-compose), reusando os testes do Financeiro.
- **F2:** auth **e-mail+senha multi-usuário** (JWT) + **Cloudflare Tunnel** no compose + **OpenAI** ligado no Super Consultor (atrás do seam `IConsultorNarrador`).
- **F3:** **web** = React do SistemaX reduzido a financeiro-only (sem sidebar) + **Visão Geral v3** + login e-mail/senha + aponta pro servidor.
- **F4:** **Tauri** desktop **Mac + Windows** (build afiado, instalador `.msi` no Windows).

## Fora de escopo agora
Gateway DigiSat, mobile (iOS/Android), Postgres, multi-tenant (é 1 negócio, N usuários).
