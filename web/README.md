# financemax — web + desktop (Tauri 2)

App financeiro-only do financemax (F3/F4): React 19 + Vite + TS reusado do Financeiro do
SistemaX, autenticado por e-mail/senha contra o `Financemax.Api` (F2). Sem PDV/Estoque/Vendas/
Clientes/Agenda — só Financeiro + login + configurações.

Empacotado como **PWA/browser** (o próprio build Vite) e como **app desktop Mac + Windows** via
**Tauri 2** (F4) — mesmo bundle React, sem reescrita: no Windows o Tauri renderiza em WebView2 (o
mesmo engine do SistemaX desktop), no Mac em WKWebView. Design idêntico ao SistemaX porque é
literalmente o mesmo React.

## Rodando o web em dev

```bash
cp .env.example .env   # ajuste VITE_API_BASE_URL se necessário
pnpm install
pnpm dev                # http://localhost:5173
```

## Configuração — `VITE_API_BASE_URL`

O client (`src/lib/api/client.ts`) sempre bate em `${VITE_API_BASE_URL}/api/...`. Não há proxy de
dev (diferente do sistemax de origem) — o servidor `Financemax.Api` roda numa VM remota atrás de
Cloudflare Tunnel, então a base URL é sempre absoluta:

| Ambiente | `VITE_API_BASE_URL` |
|---|---|
| Dev local (server via `docker compose up` / `dotnet run` em `:8080`) | `http://localhost:8080` |
| Produção (VM do dono, atrás do Cloudflare Tunnel) | `https://<subdomínio-do-túnel>.<domínio>` — ver `ARQUITETURA.md` §4 e `README.md` da raiz do repo, seção "Acesso remoto (Cloudflare Tunnel)" |

Isso vale tanto para o build web (PWA) quanto para o build desktop Tauri — o `.env` é lido em
build-time pelo Vite (`import.meta.env.VITE_API_BASE_URL`) e embutido no bundle, então **gere o
build desktop de produção com o `.env` já apontando pra URL de produção** antes de rodar
`pnpm tauri build`.

## App desktop (Tauri 2)

Estrutura adicionada em `web/src-tauri/`:

```
web/
├── src-tauri/
│   ├── Cargo.toml           crate `financemax` (lib `financemax_lib`)
│   ├── tauri.conf.json      base: identifier com.financemax.app, janela, bundle.targets ["app","dmg"]
│   ├── tauri.windows.conf.json   override merged automaticamente SÓ ao buildar no Windows: bundle.targets ["msi","nsis"]
│   ├── build.rs
│   ├── capabilities/default.json
│   ├── icons/               ícones placeholder (gerados por `tauri init`; trocar antes de ir a produção)
│   └── src/
│       ├── main.rs
│       └── lib.rs
├── package.json             scripts tauri:dev / tauri:build
└── ...
```

- **`identifier`**: `com.financemax.app`
- **`productName`** / título da janela: `financemax`
- **`build.frontendDist`**: `../dist` (output do `pnpm build`, Vite)
- **`build.devUrl`**: `http://localhost:5173` (usado por `tauri dev`, que sobe o Vite via
  `beforeDevCommand`)
- **`bundle.targets`**: split por plataforma, não uma lista única — `tauri build` valida os
  targets contra o host e falha se pedir um formato que o SO atual não sabe empacotar (`msi`/`nsis`
  exigem WiX/NSIS, que só existem em runners Windows; não há cross-build confiável de Mac→Windows).
  `tauri.conf.json` (base) fica com `["app", "dmg"]` — os dois que este Mac consegue gerar.
  `tauri.windows.conf.json` sobrescreve `bundle.targets` para `["msi", "nsis"]` e o Tauri o mescla
  **automaticamente** quando `tauri build`/`tauri dev` roda com host Windows (não precisa de flag
  nem de editar nada na hora — é resolução por plataforma do próprio Tauri, ver
  [config platform-specific](https://v2.tauri.app/reference/config/)). `bundle.windows.webviewInstallMode`
  continua na base porque é inerte em outras plataformas.

### Pré-requisitos

- Node 20+, `pnpm`
- **Rust** (via [rustup](https://rustup.rs)) — necessário para compilar o shell nativo em
  qualquer plataforma. Verifique com `cargo --version`.
- macOS: Xcode Command Line Tools (`xcode-select --install`).
- Windows: [Microsoft C++ Build Tools](https://tauri.app/start/prerequisites/#windows) +
  WebView2 Runtime (já vem no Windows 11; no 10 o instalador baixa se faltar —
  `bundle.windows.webviewInstallMode` está configurado como `downloadBootstrapper`).

### Scripts (`package.json`)

```bash
pnpm tauri:dev       # tauri dev   — sobe Vite (5173) + janela nativa com hot-reload
pnpm tauri:build     # tauri build — gera o bundle de produção pra a plataforma atual
```

### Build macOS (roda aqui, na máquina do dono que tem o toolchain Mac)

```bash
cd web
cp .env.example .env   # confirme VITE_API_BASE_URL de produção antes de buildar release
pnpm install
pnpm tauri:build
```

Gera em `web/src-tauri/target/release/bundle/`:
- `macos/financemax.app`
- `dmg/financemax_0.1.0_<arch>.dmg`

### Build Windows (roda na máquina Windows do dono — Tauri não faz cross-compile de bundle nativo)

```powershell
cd web
pnpm install
pnpm tauri:build
```

Gera em `web\src-tauri\target\release\bundle\`:
- `msi\financemax_0.1.0_x64_en-US.msi`
- `nsis\financemax_0.1.0_x64-setup.exe`

> O `.msi`/`.exe` só saem numa máquina Windows com o toolchain MSVC instalado — não há
> cross-compile de instalador nativo a partir do Mac. O código-fonte (`src-tauri/`) é o mesmo;
> só o comando `pnpm tauri:build` muda de shell (bash → PowerShell/cmd). O `tauri.windows.conf.json`
> (bundle.targets → `msi`+`nsis`) entra em vigor sozinho por rodar num host Windows — nada a mudar
> na config antes de buildar lá. O `tauri-bundler` baixa o WiX Toolset (`msi`) e o NSIS (`nsis`) na
> primeira build nativa; só é preciso ter Rust + Visual Studio Build Tools (MSVC) instalados antes.

### Assinatura de código (pendente — não bloqueia build local/teste)

Nenhuma config de code signing está presente. `pnpm tauri:build` gera `.app`/`.dmg`/`.msi`/`.nsis`
sem assinatura — ok para rodar na própria máquina/teste interno, mas:

- **macOS**: sem `bundle.macOS.signingIdentity` (certificado Apple Developer ID) + notarização, o
  Gatekeeper bloqueia abrir o `.app`/`.dmg` baixado noutra máquina ("app de desenvolvedor não
  identificado"). Contorno manual pra teste: botão direito → Abrir, ou
  `xattr -d com.apple.quarantine financemax.app`.
- **Windows**: sem certificado de assinatura Authenticode, o SmartScreen avisa "Windows protegeu
  seu PC" no instalador `.msi`/`.exe`. Contorno manual pra teste: "Mais informações" → "Executar
  assim mesmo".

Só vira bloqueador se a intenção for distribuição pública (fora da máquina do dono). Requer
certificados reais (Apple Developer Program + Authenticode) que este ambiente não tem — quando
existirem, configurar em `tauri.macos.conf.json` (`bundle.macOS.signingIdentity`) e
`tauri.windows.conf.json` (`bundle.windows.certificateThumbprint` ou `bundle.windows.sign`), nunca
committar o certificado/senha em texto — usar variáveis de ambiente do runner CI.

### Ícones

Os ícones em `src-tauri/icons/` são os placeholders gerados por `tauri init` (logo genérico do
Tauri). Trocar antes de release: gerar o set completo a partir de um PNG 1024×1024 com

```bash
pnpm tauri icon caminho/para/logo-financemax.png
```

### Verificação local (sem gerar bundle completo)

```bash
pnpm build                                  # web → dist/ (Vite + tsc)
cargo check --manifest-path src-tauri/Cargo.toml   # compila o shell Rust sem gerar bundle
```
