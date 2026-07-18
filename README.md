# financemax

**financemax** é a máquina financeira do SistemaX transformada em produto standalone: um app
financeiro-only, multiplataforma (Windows, Mac, iOS, Android e browser), que **não** tem estoque
nem cadastro próprio — ele se conecta ao sistema que o cliente já usa na operação (primeiro
integrador: **DigiSat**), ingere os fatos de negócio via um gateway instalado na máquina do
cliente, e devolve a leitura de um consultor financeiro sênior: DRE por corrente de receita,
previsão de caixa com bandas P5/P50/P95, runway, breakeven, inadimplência por roll-rate, Radar do
Simples Nacional, projetos/ROI e o **Super Consultor** (IA read-only que narra números calculados
deterministicamente — nunca calcula, nunca age). O servidor roda na VM do dono (Docker +
Cloudflare Tunnel, sem porta exposta); a UI é a mesma do Financeiro do SistemaX (React + Tailwind,
tema claro), idêntica no desktop e responsiva no mobile.

> **Comece por [`ARQUITETURA.md`](./ARQUITETURA.md)** — visão geral, análise de stack (incluindo o
> veredito sobre Flutter), auth, IA, deploy, o mapa reuso-vs-novo e o plano de fases. Este repo
> ainda está na fase de blueprint: nenhum código de app foi escrito.

## Organização planejada do repo

```
ARQUITETURA.md            ← o contrato (leia primeiro)
src/                      ← backend .NET 10 (F1+)
  SharedKernel/             Money (centavos), Result, AggregateRoot — fork do SistemaX
  Modules/
    Abstractions/           IModule, eventos, ledger, RBAC, contratos do Consultor
    Financeiro/             Domain / Application (Quant, ReadModels, Analitico) / Infrastructure.Sqlite
    Identidade/             Usuario (e-mail+senha Argon2id, JWT+refresh) — adaptado
  Contratos/                Financemax.Contratos — eventos canônicos servidor↔gateway
  Hosts/
    Financemax.Api/         composition root Linux: API + webhook ingest + IA + PWA estática
  Gateway/
    Financemax.Gateway/     worker .NET na máquina do cliente: lê MongoDB do DigiSat → webhook
apps/
  app/                      React 19 + Vite + TS + Tailwind (UI do Financeiro do SistemaX)
  desktop/                  shell Tauri 2 (Windows/macOS)
  mobile/                   shell Capacitor (iOS/Android)
deploy/
  docker-compose.yml        api + cloudflared (túnel) + litestream (backup SQLite)
tests/                      suíte do Financeiro (herdada, verde no CI Linux) + novos
docs/                       ADRs e notas do financemax
```

## Fases (resumo)

1. **F1** — extrair o backend financeiro do SistemaX como serviço headless em Docker.
2. **F2** — auth e-mail+senha multi-tenant, webhook de ingestão, Cloudflare Tunnel na VM.
3. **F3** — app desktop/web reusando o React do SistemaX (Tauri Win/Mac + PWA).
4. **F4** — gateway DigiSat (MongoDB → eventos canônicos, outbox offline, Velopack).
5. **F5** — mobile responsivo (Capacitor iOS/Android) + Super Consultor com LLM ligado.
