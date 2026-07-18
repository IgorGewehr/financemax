# financemax — Arquitetura e Escolha de Stack

> **Status:** blueprint (2026-07). Nenhuma linha de código de app ainda — este documento é o contrato.
> **Missão:** extrair a máquina financeira do SistemaX (motor quant + DRE por corrente + projetos/ROI
> + Radar do Simples + Super Consultor) para um app **financeiro-only, multiplataforma**, que se
> comunica com sistemas externos (primeiro: **DigiSat**) em vez de ter estoque/clientes próprios.
> O DigiSat opera a loja; o financemax enxerga o dinheiro como um consultor sênior.

Fontes de reuso (read-only): `/Users/igorgewehr/air/sistemax` — em especial
`src/Modules/Financeiro/`, `src/SharedKernel/`, `src/Modules/Abstractions/`, `web/src/` e
`docs/financeiro/inteligencia-arquitetura.md`.

---

## 1. Visão geral — 3 componentes

| Componente | O que é | Onde roda |
|---|---|---|
| **App** | UI React (a mesma do Financeiro do SistemaX) empacotada para Win/Mac/iOS/Android + web | Máquina/celular do usuário |
| **Servidor** | Backend financeiro .NET (o motor do SistemaX, headless) + webhook de ingestão + camada de IA | VM do dono, Docker + Cloudflare Tunnel |
| **Gateway** | Agente instalado na máquina do cliente que lê o banco do DigiSat e envia eventos ao webhook | Máquina do cliente (Windows service) |

### 1.1 Fluxo de dados (diagrama textual)

```
MÁQUINA DO CLIENTE                      VM DO DONO (Docker, sem porta aberta)              DISPOSITIVOS DO USUÁRIO
──────────────────                      ─────────────────────────────────────              ───────────────────────

DigiSat (PDV/estoque/clientes)
  └─ MongoDB local
     ("MongoDBDigisat")
        │  leitura read-only
        ▼  (poll incremental + backfill)
Financemax.Gateway                          ┌──────────────────────────────────┐
  worker .NET, Windows service              │            cloudflared           │
  mapeia docs DigiSat → eventos             │   (túnel outbound-only, TLS na   │
  canônicos + ChaveIdempotencia             │        borda da Cloudflare)      │
  outbox local (SQLite) p/ offline          └───────────────┬──────────────────┘
        │                                                   │
        └── HTTPS + HMAC ──▶  POST /api/ingest/eventos ─────┤
                                                            ▼
                                            Financemax.Api (ASP.NET Core, .NET 10)
                                              │ 1. valida HMAC + janela anti-replay
                                              │ 2. grava no LEDGER append-only
                                              │    integration_events (ULID,
                                              │    UNIQUE chave_idempotencia)
                                              ▼
                                            Projeções com cursor (fold determinístico)
                                              ▼
                                            FACT TABLES  fato_caixa_diario ·
                                              fato_receita_diaria · fato_recebiveis ·
                                              fato_custo_diario · fato_margem_produto
                                              ▼                          ▼
                                            Read-models             Motor Quant
                                            (DRE por corrente,      (bandas P5/P50/P95,
                                             fluxo, ROI, projetos)   runway, breakeven,
                                              │                      roll-rate, Radar
                                              │                      do Simples, TIR)
                                              ▼                          ▼
                                            Super Consultor (facts → LLM barato →
                                              cache por hash → fallback template)
                                              │
        ┌─────────────────────────────────────┘
        │  GET /api/financeiro/*  (Bearer JWT, e-mail+senha)
        ▼
App React (design idêntico ao Financeiro do SistemaX)
  ├─ Desktop Win/Mac ... Tauri 2 (WebView2 no Windows / WKWebView no Mac)
  ├─ iOS/Android ...... Capacitor (mesmo bundle Vite, passe responsivo)
  └─ Browser .......... PWA (mesma origem do servidor)
```

Princípio herdado e mantido: **insumo é evento, nunca leitura direta de tabela alheia**. O DigiSat
nunca é consultado pelo servidor; o gateway traduz o estado do DigiSat em *fatos de negócio*
(`VendaConcluida`, `ContaLancada`, `ParcelaPaga`, ...) e o servidor só conhece o ledger. Isso é o
que torna o financemax agnóstico de origem: um segundo ERP de origem = um segundo adapter no
gateway, zero mudança no servidor.

---

## 2. Análise de stack — recomendação por componente

### 2.1 Resumo executivo

| Componente | Recomendação | Alternativa descartada |
|---|---|---|
| App (UI) | **Reusar o React do SistemaX** (React 19 + Vite + TS + Tailwind) | Flutter (ver veredito §2.2) |
| App desktop Win/Mac | **Tauri 2** embrulhando o build Vite | Electron (fallback documentado) |
| App iOS/Android | **Capacitor** embrulhando o mesmo build Vite | Tauri mobile (ainda foundational) |
| Servidor | **.NET 10 / ASP.NET Core** — montar o módulo Financeiro do SistemaX headless | Reescrever o motor em TS/Python |
| Banco (dados do tenant) | **SQLite por tenant** (reusa `Infrastructure.Sqlite` inteira) + caminho Postgres documentado | Postgres já na F1 (custo de porte sem ganho no nosso volume) |
| Gateway | **.NET 10 Worker Service** + `MongoDB.Driver`, instalado via Velopack | Go/Node (perde o compartilhamento de contratos) |
| IA | **LLM barato server-side** atrás do seam `IConsultorNarrador` já existente | LLM caro / IA que calcula |
| Infra | **Docker Compose + cloudflared + Litestream** na VM do dono | Expor porta / VPS gerenciado |

### 2.2 O veredito sobre Flutter (honesto)

**Não use Flutter para o financemax.** Não é uma questão de gosto — é aritmética de ativos:

1. **O ativo mais valioso do projeto já existe em React.** O Financeiro do SistemaX tem 9 telas
   construídas à risca de mockups aprovados (`docs/ui/financeiro-ui.md` — "o mockup é o contrato"),
   um design system tokenizado (`design-tokens.css`, HSL, estados semânticos pos/warn/crit,
   JetBrains Mono nos números), gráficos SVG custom (`timelineGeometry.ts`, recharts), o padrão
   `Recurso<T>` + `calc.ts`/`calc.test.ts` por tela, e adapters tipados. Flutter joga **100% disso
   fora** e reescreve em Dart — semanas/meses de retrabalho para chegar, na melhor hipótese, no
   mesmo lugar.
2. **O requisito "no desktop o design é LITERALMENTE o mesmo do SistemaX" é impossível de cumprir
   barato em Flutter.** O SistemaX desktop renderiza React dentro de WebView2. Em Flutter, "o mesmo
   design" vira uma reimplementação manual pixel a pixel de um design system que continuará
   evoluindo no SistemaX — dois sistemas visuais divergindo para sempre, sem nenhum pacote
   compartilhável entre Dart e TS. Com React + Tauri, no Windows o app roda **no mesmo engine
   (WebView2)** que o SistemaX usa — é literalmente o mesmo render, de graça.
3. **O ganho de Flutter não se aplica a este app.** Native-feel de Flutter paga quando o app é
   mobile-first com interação nativa pesada (câmera, mapas, animação 60fps, gestos complexos).
   O financemax é um dashboard financeiro: cards, listas, formulários, gráficos SVG. Essa classe
   de app roda impecável em WebView moderna — o "contra" do webview é real em jogos, não em DRE.
4. **Custo de manutenção solo.** Um dono, dois codebases de UI (Dart no financemax, TS no
   SistemaX) para o mesmo design é o pior cenário possível. Reusar o React mantém **um** design
   system: melhoria numa tela do SistemaX é um cherry-pick no financemax e vice-versa.

Quando Flutter *seria* a resposta: se não existisse UI pronta, se o mobile fosse o alvo primário
com features nativas profundas, e se o requisito de paridade com o SistemaX não existisse. Nenhuma
das três condições vale aqui.

### 2.3 App — detalhe da recomendação

**Base:** SPA React 19 + Vite + TypeScript + Tailwind, extraída de `sistemax/web` (design tokens,
`components/ui`, `components/financial/**`, `pages/financeiro/**`, `lib/api`). **Tema claro é o
default por construção** — os tokens do SistemaX já são light-first (`:root` = claro, `.dark` é
opt-in via classe), então o requisito "tema claro primeiro" já está atendido pelo reuso.

**Um build, quatro empacotamentos:**

| Alvo | Shell | Por quê |
|---|---|---|
| Windows | **Tauri 2** | Renderiza em WebView2 — o MESMO engine do SistemaX desktop → paridade visual literal. Binário ~10 MB, updater embutido, assinatura de código suportada. |
| macOS | **Tauri 2** | WKWebView; o CSS do app é Tailwind padrão (flex/grid/vars), sem dependência de quirk de engine. Mesmo pipeline do Windows. |
| iOS / Android | **Capacitor** | Empacotamento mobile maduro: pipeline de loja estabelecido, plugin ecosystem (push, biometria, secure storage p/ refresh token), live-reload. Tauri 2 mobile é estável mas o próprio time o descreve como fundação, não história completa — não vale ser early adopter aqui. |
| Browser | **PWA** servida pelo próprio Financemax.Api | Custo zero (mesmo bundle), útil para o contador/sócio que não instala nada. |

- **Responsividade mobile é um passe de CSS, não um port**: as telas ganham variantes
  `sm:`/`md:` (sidebar → bottom-nav, tabelas → cards empilhados, touch targets ≥ 44px,
  `safe-area-inset`). O trabalho fica nas telas, não na arquitetura.
- **Trade-off admitido (webview no mobile):** scroll/tela de dashboard em WebView moderna é
  indistinguível de nativo para este domínio; o risco real são listas gigantes — mitigado com
  virtualização (mesmo padrão `react-virtuoso` já validado no saas-erp). Se um dia uma tela
  específica exigir nativo de verdade, Capacitor permite view nativa pontual sem reescrever o app.
- **Fallback documentado:** se algum quirk do WKWebView (Mac) doer em produção, trocar o shell
  desktop por Electron custa um dia (mesmo bundle web) ao preço de ~100 MB por instalação.
  Decisão reversível — mais um motivo para não temer o caminho web.
- **O que muda vs `sistemax/web`:** o `lib/api/client.ts` troca boot-token+PIN por
  e-mail+senha+refresh (§4) e ganha base URL absoluta configurável (`https://api.financemax...`);
  as telas de PDV/Estoque/Vendas/OS **não vêm** — só o Financeiro + login + configurações.

### 2.4 Servidor — reusar o motor .NET (a decisão que evita reescrever a matemática)

**Sim, reusar — e é mais barato do que parece.** O módulo Financeiro do SistemaX já é, por
arquitetura, um serviço headless esperando um host:

- `Modules/Financeiro/{Domain,Application,Infrastructure}` é **.NET 10 puro, cross-platform**
  (nada de Windows/Photino ali — isso vive só no `Host.Desktop`, que não vem).
- Os endpoints HTTP **já estão escritos** (`FinanceiroEndpointsModule` — ASP.NET Minimal APIs
  registradas via `IModule`/`IModuleEndpoints`). Montar num container Linux é escrever um
  composition root novo (`Financemax.Api`), não portar código.
- A matemática que não podemos nos dar ao luxo de reescrever vem inteira:
  `Application/Quant/` (BandasDeFluxoDeCaixa, RunwayCalculator, BreakevenMensal,
  InadimplenciaRollRate, RadarDoSimplesNacional, TaxaInternaDeRetorno, MatematicaDePayback,
  SeedDeterministico...), `Application/ReadModels/` (DreGerencialService com a dimensão
  `CorrenteDeReceita`, FluxoDeCaixa, PrevisaoDeCaixa, RoiDoNegocio, QuantoSobrouDeVerdade,
  RadarDoSimples...), `Application/Analitico/` (fact tables + projeções com cursor),
  `Application/Projetos/` e o ledger (`integration_events`, persist-then-dispatch,
  idempotência por ULID). Tudo coberto pela suíte de ~709 testes que continua rodando no CI
  do financemax.
- `SharedKernel` (Money em centavos, Result, AggregateRoot) e `Modules.Abstractions`
  (IModule, catálogo de eventos, contratos do Consultor, RBAC `Papel`) vêm junto.

**Banco: SQLite por tenant, de propósito.** `Infrastructure.Sqlite` (repos + 40 migrations) é
reusada byte a byte: cada cliente ganha `tenants/{tenantId}.db` (WAL) num volume Docker, mais um
`control.db` (tenants, usuários, credenciais de gateway, budget de IA). Justificativa honesta:

- **Zero porte na F1** — portar ~30 repositórios SQL para Postgres é semanas de trabalho sem
  nenhum ganho no volume real (uma VM, dezenas de tenants, escrita = rajadas do gateway).
- **Isolamento de tenant por construção** — um arquivo por CNPJ é o R1 (businessId sagrado)
  na forma mais forte possível: não existe query capaz de vazar entre tenants.
- **Backup contínuo** com Litestream (replicação WAL → R2/B2) por arquivo.
- **Caminho de saída documentado**: os repos são ports (`IContaAReceberRepository`, ...);
  quando um tenant grande ou BI multi-tenant justificar, escreve-se `Infrastructure.Postgres`
  atrás dos mesmos ports — decisão contida, não reengenharia. O `docker-compose` já reserva o
  slot do Postgres.

**Mecânica de extração (F1):** copiar (não submodular) `SharedKernel`, `Modules/Abstractions`,
`Modules/Financeiro` e seus testes para o repo financemax. É um fork consciente: o financemax
passa a ser dono da sua cópia (o SistemaX segue o caminho dele de ERP local-first; os dois
divergem por design). Melhorias de matemática podem ser cherry-pickadas nos dois sentidos.

### 2.5 Gateway DigiSat — .NET Worker Service

**O que sabemos do DigiSat** (verificado): é a suíte de automação comercial da DigiSat Tecnologia
(Sistema Comercial / Gerencial / Administrador) e roda com **MongoDB local** na máquina do cliente
— os serviços Windows `MongoDBDigisat` e `SincronizadorDigisat` aparecem na documentação de
suporte oficial. Ou seja: o gateway lê um MongoDB em `localhost`, não Firebird/SQL Server.
(Validar na F4, na máquina real do cliente: porta, auth, nomes de collections e se o Mongo roda
standalone — standalone não tem change streams, o que já assumimos abaixo.)

**Stack: .NET 10 Worker Service** (`Financemax.Gateway`), pelos motivos:

- **Compartilha o pacote de contratos** (`Financemax.Contratos`: DTOs dos eventos canônicos +
  regra de `ChaveIdempotencia`) com o servidor — impossível em Go/Node sem duplicação.
- `UseWindowsService()` de primeira classe, publish single-file, e o know-how de instalador
  **Velopack já existe** no SistemaX (ADR-0004) — auto-update do gateway resolvido.
- `MongoDB.Driver` oficial e maduro.

**Comportamento:**

1. **Backfill** inicial (histórico de vendas/contas) + **incremental por polling** com cursor
   persistido (`_id`/timestamps — sem depender de change streams, que exigem replica set).
2. **Mapeia** documentos DigiSat → eventos canônicos do financemax (`VendaConcluida` com forma de
   pagamento/parcelas, `ContaLancada`, `ParcelaPaga`, `CompraRecebida`...), cada um com
   `ChaveIdempotencia` derivada do id de origem (`digisat:{collection}:{_id}:{versao}`) — replay
   e re-envio são seguros por construção (UNIQUE no ledger).
3. **Outbox local** (SQLite) — internet caiu, eventos acumulam; voltou, drena em lote com retry
   exponencial. O gateway nunca perde fato nem duplica.
4. **Envia** `POST /api/ingest/eventos` em lote, assinado com HMAC-SHA256 (segredo por
   instalação) + timestamp anti-replay.
5. **Port `IFonteDeDados`** no desenho desde o dia 1: o adapter DigiSat/Mongo é o primeiro;
   um futuro cliente com outro ERP = outro adapter, mesmo gateway.

### 2.6 IA — Super Consultor

Reusa o desenho já especificado e parcialmente construído no SistemaX
(`docs/financeiro/inteligencia-arquitetura.md` + contratos em `Modules.Abstractions/Consultor/`),
com uma **simplificação estrutural**: no SistemaX o pipeline é split (dados locais no desktop,
LLM na nuvem); no financemax **os dados já moram no servidor**, então o pipeline inteiro roda
num lugar só.

Princípios mantidos (inegociáveis):

- **LLM é narrador, não analista.** Toda matemática é determinística em C# (motor quant). O LLM
  recebe facts **já formatados** (`"R$ 4.200,00"`) e devolve frases; nunca vê linha crua do banco
  (privacidade e alucinação resolvidas no mesmo movimento).
- **IA é read-only** (Lei 2 do contrato de UI): observa, explica, aconselha, com drill de
  navegação — nunca CTA de ação.
- **Falha nunca vira erro na UI**: pior caminho = `source: "template"` (frase determinística
  interpolada, sempre renderizável). Cache por `sha256(facts)`, budget mensal por tenant com
  circuit breaker, validação anti-alucinação (números da frase ⊆ números dos facts).

Pipeline por tenant (hosted service no Financemax.Api): cron 06:00 `America/Sao_Paulo` →
`IConsultorFactProvider` coleta ~15–30 fatos → ranking determinístico por Score → top-N →
`IConsultorNarrador` (implementação LLM) → `consultor_insights` → `GET /api/consultor/insights?tela=X`.

**Custo (provado no estudo do SistemaX):** modelo barato classe gpt-4o-mini/Haiku, ~1.900 tokens
in + ~650 out por run de 8 insights → **≈ R$ 0,11/mês nominal, ≈ R$ 0,78/mês pessimista por
CNPJ**. O seam `IConsultorNarrador` deixa o modelo ser config, não arquitetura. Headroom no teto
(~300 chamadas/mês) banca a evolução da F5+: chat "pergunte ao consultor" com tools read-only
sobre os read-models (~150 perguntas/mês dentro do mesmo teto).

---

## 3. Auth — e-mail + senha, multi-tenant

O `Usuario` do SistemaX (ADR-0003) **já tem `Email`, `BusinessId`, `Papel` (RBAC) e `Status`** —
o modelo vem inteiro; o que muda é a credencial (PIN local → senha remota) e a sessão (boot-token
de janela → JWT com refresh):

| Aspecto | Decisão |
|---|---|
| Credencial | E-mail + senha. Hash **Argon2id** (novo `SenhaHasher`, mesmo shape do `PinHasher` PBKDF2 existente — o PIN não vem). |
| Sessão | JWT access curto (~15 min, claims `sub`, `tenantId`, `papel`) + **refresh token rotativo** revogável (tabela em `control.db`). Web/PWA: cookie httpOnly; Tauri/Capacitor: secure storage nativo. |
| Multi-tenant | `tenantId` no token → resolve o arquivo `tenants/{tenantId}.db`. Isolamento físico por tenant (§2.4). Um usuário pertence a um tenant; contador com N clientes = N vínculos (fase futura, modelo já suporta via tabela de vínculo). |
| RBAC | Reusa `Papel`/`Permissoes` de `Modules.Abstractions.Autorizacao` (dono opera; contador/sócio = viewer). |
| Proteções | Rate-limit no login, lockout progressivo, senha mínima com verificação de trivialidade (análogo ao `PinTrivial`), reset por e-mail (Resend/SES, F5), TOTP opcional futuro. |
| Gateway (máquina-a-máquina) | **Não usa** JWT de usuário: credencial própria por instalação (id + segredo HMAC), emitida no provisionamento do tenant, revogável no `control.db`. |

---

## 4. Deploy — Docker + Cloudflare Tunnel na VM do dono

```yaml
# deploy/docker-compose.yml (esqueleto-alvo)
services:
  api:            # Financemax.Api (ASP.NET Core) — serve API + PWA estática
    image: ghcr.io/<igor>/financemax-api:latest
    volumes: [ "data:/data" ]        # /data/control.db + /data/tenants/*.db (SQLite WAL)
    environment:
      - Financemax__DataDir=/data
      - Financemax__Llm__ApiKey=${LLM_API_KEY}
      - Financemax__Jwt__SigningKey=${JWT_SIGNING_KEY}
    # SEM ports: — ninguém fala com a api exceto pelo túnel

  cloudflared:    # túnel outbound-only: a VM não abre NENHUMA porta de entrada
    image: cloudflare/cloudflared:latest
    command: tunnel run
    environment: [ "TUNNEL_TOKEN=${CLOUDFLARE_TUNNEL_TOKEN}" ]

  litestream:     # backup contínuo dos SQLite (WAL streaming) para R2/B2
    image: litestream/litestream
    command: replicate
    volumes: [ "data:/data", "./litestream.yml:/etc/litestream.yml" ]

volumes: { data: {} }
```

- **Rotas no túnel:** `app.<domínio>` → PWA + API do app (JWT) · `ingest.<domínio>` → webhook
  (HMAC) · `admin.<domínio>` → provisionamento de tenants, protegido adicionalmente por
  **Cloudflare Access** (Zero Trust, e-mail do dono).
- TLS termina na borda da Cloudflare; o túnel é outbound-only → **a VM fica sem IP/porta
  exposta**, exatamente o requisito.
- **Atualização:** CI (GitHub Actions) builda a imagem → GHCR → `docker compose pull && up -d`
  na VM (ou Watchtower). App desktop atualiza via updater do Tauri; gateway via Velopack.
- **Observabilidade mínima de F2:** `/api/health` com versão (padrão já existente no SistemaX),
  logs estruturados no stdout (`docker logs`), alerta de falha de ingestão por e-mail.

---

## 5. O que se REUSA do SistemaX vs o que é NET-NEW

### Reusa como está (fork consciente, testes junto)

| Peça | De onde |
|---|---|
| Money (centavos), Result, AggregateRoot, DomainEvent | `src/SharedKernel/` |
| IModule/ModuleRegistry, catálogo de eventos, ledger (`IIntegrationEventLedgerStore`, `IProjection`), RBAC, contratos do Consultor | `src/Modules/Abstractions/` |
| Motor quant completo (bandas, runway, breakeven, roll-rate, Radar do Simples, TIR, payback, seed determinística) | `Financeiro.Application/Quant/` |
| Read-models: DRE por corrente (`CorrenteDeReceita`), fluxo, previsão, ROI do negócio, projetos, Radar, inadimplência... | `Financeiro.Application/ReadModels/` + `Projetos/` |
| Fact tables + projeções com cursor | `Financeiro.Application/Analitico/` |
| Domínio: LancamentoContabil (partida dobrada), ContaAPagar/Receber, MovimentoFinanceiro, Assinatura, Projeto, AtivoDeCapital, FSMs | `Financeiro.Domain/` |
| Persistência SQLite inteira (repos + 40 migrations) | `Financeiro.Infrastructure/Sqlite/` |
| Endpoints HTTP do Financeiro (Minimal APIs) | `Financeiro.Application/Endpoints/` |
| Suíte de testes do Financeiro (subset dos ~709) | `tests/SistemaX.Modules.Financeiro.Tests` |
| Design system light-first (tokens HSL, pos/warn/crit, fontes) + componentes `ui/` | `web/src/styles/` + `web/src/components/ui/` |
| 9 telas do Financeiro + calc.ts/testes + adapters + `Recurso<T>` | `web/src/pages/financeiro/` + `web/src/components/financial/` + `web/src/lib/api/` |
| Contrato de UI (mockups como spec, Lei 1/Lei 2) | `docs/ui/financeiro-ui.md` + mockups |
| Know-how Velopack (instalador/auto-update do gateway) | ADR-0004 + `build/pack-windows.ps1` |

### Adapta (mesmo shape, credencial/host novos)

- **Identidade**: `Usuario` mantém Email/Papel/BusinessId; PIN → Argon2id + JWT/refresh (§3).
- **`lib/api/client.ts`**: boot-token/PIN → e-mail+senha+refresh; base URL absoluta.
- **Consultor**: pipeline split local/nuvem → pipeline único server-side (§2.6).
- **Composition root**: `Host.Desktop` (Photino/Windows) → `Financemax.Api` (container Linux).

### Net-new (não existe em lugar nenhum)

- `POST /api/ingest/eventos` (webhook: HMAC, anti-replay, lote, resultado por item).
- `Financemax.Contratos` — pacote compartilhado servidor↔gateway (eventos canônicos + regra de idempotência).
- **`Financemax.Gateway`** completo: adapter Mongo/DigiSat, mapeador → eventos, cursor, outbox, instalador.
- Provisionamento de tenant (`control.db`, credencial de gateway, budget IA) + painel `admin.`.
- Shells **Tauri** (Win/Mac) e **Capacitor** (iOS/Android); passe responsivo mobile das telas.
- Reset de senha por e-mail; (futuro) billing e vínculo contador→N tenants.

---

## 6. Plano de fases

> Regra de corte: cada fase termina com um critério observável, não com "código pronto".

**F1 — Extrair o backend financeiro headless (a fase que destrava tudo)**
Novo repo: copiar SharedKernel + Abstractions + Financeiro (Domain/Application/Infrastructure.Sqlite)
+ testes; criar `Financemax.Api` (composition root Linux) montando `FinanceiroEndpointsModule`;
Dockerfile.
✅ *Pronto quando:* `docker run` na VM responde `/api/health` e `/api/financeiro/*` com seed demo,
e a suíte de testes do Financeiro está verde no CI Linux.

**F2 — Servidor de verdade: auth + tenancy + webhook + túnel**
Identidade adaptada (Argon2id, JWT+refresh), `control.db` + SQLite por tenant, provisionamento de
tenant + credencial de gateway, `POST /api/ingest/eventos` (HMAC, idempotência via ledger),
compose com cloudflared + Litestream no ar.
✅ *Pronto quando:* login e-mail+senha de qualquer lugar do mundo funciona; um evento postado no
webhook aparece na fact table e na resposta da API do tenant certo — e só dele.

**F3 — App desktop/web reusando o React**
Extrair design system + telas Financeiro de `sistemax/web` para `apps/app`; trocar client
(e-mail+senha, base URL); tela de login nova; PWA servida pelo Api; shells Tauri Win/Mac com
updater. Tema claro default (já é).
✅ *Pronto quando:* o app instalado no Windows/Mac é visualmente indistinguível do Financeiro do
SistemaX, logado por e-mail/senha, exibindo dados vindos do servidor.

**F4 — Gateway DigiSat**
`Financemax.Contratos` + worker .NET: descoberta do schema real na máquina do cliente (collections,
porta, auth do `MongoDBDigisat`), adapter + mapeador → eventos canônicos, backfill + incremental
com cursor, outbox offline, instalador Velopack como Windows service.
✅ *Pronto quando:* a máquina real do cliente alimenta o dashboard sozinha; derrubar a internet
por 1h não perde nem duplica um evento (prova do outbox + idempotência).

**F5 — Mobile responsivo + IA ligada**
Capacitor iOS/Android (secure storage p/ refresh, safe-area, bottom-nav, tabelas→cards,
virtualização onde precisar); Super Consultor com LLM real (narrador + cache + budget +
anti-alucinação + fallback template); depois, chat read-only "pergunte ao consultor".
✅ *Pronto quando:* app no TestFlight/APK usável com uma mão; consultor narrando os números reais
do cliente dentro do teto de custo (~R$ 1/mês/CNPJ).

*(Contínuo, não fase: backup Litestream verificado com restore de teste, alerta de ingestão parada,
rotação de segredos.)*

---

## 7. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Schema do DigiSat diferente do assumido (collections/auth/versão) | F4 começa com sessão de descoberta na máquina real; adapter isolado atrás de `IFonteDeDados`; nada do servidor depende do formato DigiSat |
| Mongo do DigiSat standalone sem change streams | Já assumido: polling incremental com cursor (não é otimização futura, é o design) |
| WKWebView (Mac) com quirk visual | Fallback Electron documentado (§2.3) — mesmo bundle, troca de shell em ~1 dia |
| Tauri mobile amadurecer e valer a unificação | Capacitor e Tauri embrulham o MESMO build Vite — trocar shell mobile depois é barato; nada no app depende do shell |
| SQLite por tenant atingir limite (tenant gigante/BI cross-tenant) | Ports já isolam a persistência; `Infrastructure.Postgres` é adição contida; slot no compose reservado |
| Divergência do fork vs SistemaX | Aceita por design; melhorias de matemática cherry-pickadas caso a caso (mesma linguagem, mesma estrutura de pastas torna isso trivial) |

---

## 8. Referências

- SistemaX: `docs/financeiro/inteligencia-arquitetura.md` (ledger, fact tables, Consultor, custo),
  `docs/ui/financeiro-ui.md` (contrato de UI), `docs/arquitetura/` (ADRs 0001–0005),
  `src/Modules/Financeiro/`, `web/src/`.
- DigiSat usa MongoDB local (serviços `MongoDBDigisat`/`SincronizadorDigisat`):
  [FAQ oficial DigiSat](https://www.digisat.com.br/faqs/categoria/outras) ·
  [Sistema Comercial](https://www.digisat.com.br/sistemas/detalhes/sistema-comercial/sistema) ·
  [Sistema Gerencial](https://www.digisat.com.br/sistemas/detalhes/sistema-gerencial/sistema)
- Tauri 2 (desktop estável; mobile estável porém foundational):
  [Tauri 2.0 RC blog](https://v2.tauri.app/blog/tauri-2-0-0-release-candidate/) ·
  [Capacitor vs Tauri (2026)](https://trysaasbattle.com/tauri-vs-capacitor/) ·
  [buildwith.app comparativo](https://buildwith.app/compare/capacitor-vs-tauri)
