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

1. **F1 ✅** — backend financeiro do SistemaX extraído como serviço headless em Docker (SQLite, 806 testes).
2. **F2 ✅** — auth e-mail+senha multi-usuário ONLINE (JWT+refresh, Argon2id, RBAC, lockout) + Cloudflare Tunnel.
3. **F3** — app desktop/web reusando o React do SistemaX (Tauri Win/Mac + PWA).
4. **F4** — gateway DigiSat (MongoDB → eventos canônicos, outbox offline, Velopack).
5. **F5** — mobile responsivo (Capacitor iOS/Android) + Super Consultor com LLM ligado.

## Rodando localmente

```bash
cp .env.example .env
# edite .env: gere FINANCEMAX_JWT_SECRET com `openssl rand -base64 48`
docker compose up -d --build
curl http://localhost:8080/api/health
```

No PRIMEIRO boot, o servidor semeia um usuário **founder** (`admin@financemax.local` por padrão,
configurável via `FINANCEMAX_ADMIN_EMAIL_INICIAL`) com senha inicial **gerada aleatoriamente e
impressa uma única vez no log** (`docker compose logs api | grep "usuário administrador"`) —
ou uma senha conhecida, se você preencher `FINANCEMAX_ADMIN_SENHA_INICIAL` no `.env` antes do
primeiro boot. O login devolve `mustChangePassword: true`: troque a senha assim que entrar
(`PATCH /api/usuarios/{id}` com `novaSenha`).

### Endpoints de auth

| Rota | O que faz |
|---|---|
| `POST /api/auth/login` | `{ email, senha }` → `{ accessToken, refreshToken, expiraEm, usuario }` |
| `POST /api/auth/refresh` | `{ refreshToken }` → novo par (rotaciona; o antigo é revogado — reuso derruba a sessão inteira) |
| `POST /api/auth/logout` | `{ refreshToken }` → revoga (204) |
| `POST /api/usuarios` | admin/founder cria usuário — `{ nome, email, senha, papel }` |
| `GET /api/usuarios` | admin/founder lista usuários do negócio |
| `PATCH /api/usuarios/{id}` | admin/founder edita `papel`/`ativo`/`novaSenha` (reset) |

Todo o resto de `/api/*` (Financeiro, etc.) exige `Authorization: Bearer <accessToken>` — sem
token válido, 401; com token válido mas papel sem a permissão do módulo, 403.

## Acesso remoto (Cloudflare Tunnel)

O servidor roda na máquina/VM do dono **sem nenhuma porta aberta para a internet** — o acesso do
colega (Windows) é via **Cloudflare Tunnel** (outbound-only, TLS na borda da Cloudflare).

**No lado do dono (uma vez):**

1. No [painel Cloudflare Zero Trust](https://one.dash.cloudflare.com/) → *Networks* → *Tunnels* →
   crie um túnel, aponte o *Public Hostname* para o serviço `api:8080` (rede interna do compose).
2. Copie o **token** do túnel e cole em `CLOUDFLARE_TUNNEL_TOKEN` no `.env` (nunca commitado — é
   gitignored; só o `.env.example` com o placeholder vazio vai pro repo).
3. Suba o serviço opcional `cloudflared` junto com a API:
   ```bash
   docker compose --profile tunnel up -d
   ```
   Sem `--profile tunnel`, o `docker compose up` de sempre continua subindo só a `api` (acesso
   local/LAN pela porta publicada) — o túnel é estritamente aditivo.

**No lado do colega (Windows):** nenhuma instalação além do app/cliente HTTP — ele só precisa da
**URL pública** que a Cloudflare deu ao túnel (ex. `https://financemax.seu-dominio.com`) e das
credenciais de um usuário criado por você (`POST /api/usuarios`, papel `Operator`/`Viewer`/`Admin`
conforme o que ele deve poder fazer). O app aponta a base URL da API para essa URL do túnel; o
login (`POST /api/auth/login`) e o resto do fluxo funcionam exatamente como local — a única
diferença é que o tráfego atravessa a borda da Cloudflare em vez da LAN.
