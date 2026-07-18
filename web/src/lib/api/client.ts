/**
 * Cliente HTTP tipado do Financemax.Api (§3/§4 do prompt da F3 — ver `ARQUITETURA.md` do
 * financemax raiz). Diferente do `sistemax/web` de origem (boot-token + PIN local, mesma origem
 * do Kestrel embutido): aqui a base URL é ABSOLUTA e configurável (`VITE_API_BASE_URL`, ver
 * `.env.example`) porque o servidor mora numa VM remota atrás de Cloudflare Tunnel, não no mesmo
 * processo do app. Sessão é um PAR access+refresh (JWT, ver `IdentidadeEndpointsModule` no
 * servidor .NET) em vez de um único token de boot — `request()` tenta `POST /auth/refresh`
 * automaticamente num 401 antes de desistir, e só devolve a UI pro login se o refresh também
 * falhar.
 */

export interface Money {
  centavos: number;
  moeda: string;
}

export function moneyToReais(m: Money | null | undefined): number {
  return (m?.centavos ?? 0) / 100;
}

export function reaisToCentavos(reais: number): number {
  return Math.round(reais * 100);
}

/** Sem barra final — `request()` sempre concatena `${BASE_URL}/api${path}` com `path` começando
 * em `/`. RELATIVA por padrão (string vazia): tanto em dev (`vite.config.ts` faz proxy de `/api`
 * pro `Financemax.Api` local) quanto em produção (o túnel do dono — Cloudflare Tunnel/reverse
 * proxy na VM — roteia `/api` da MESMA origem que serve o build estático, ex.
 * `finance.tensorroot.com/api/...`) o front nunca precisa saber o host/porta real do servidor —
 * zero CORS, zero URL absoluta hardcoded no bundle. `VITE_API_BASE_URL` continua existindo como
 * OVERRIDE opcional (ex.: apontar um front local pra uma API remota sem tocar no túnel) — ver
 * `.env.example`. */
const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '');

const SESSION_KEY = 'financemax:session';

export interface UsuarioSessao {
  id: string;
  nome: string;
  email: string;
  papel: string;
  ativo: boolean;
  mustChangePassword: boolean;
}

/** Espelha `TokensResponse` (.NET, `IdentidadeEndpointsModule`) — `accessToken` curto (~15 min) +
 * `refreshToken` rotativo. `expiraEm` é do ACCESS token (o refresh tem validade própria no
 * servidor; o cliente não precisa saber quando ele vence, só reagir a um 401 do refresh). */
export interface Session {
  accessToken: string;
  refreshToken: string;
  expiraEm: string;
  usuario: UsuarioSessao;
}

export function readSession(): Session | null {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Session;
    if (!parsed.accessToken || !parsed.refreshToken) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function writeSession(session: Session | null): void {
  if (session) localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  else localStorage.removeItem(SESSION_KEY);
}

export class ApiError extends Error {
  readonly codigo: string;
  readonly status: number;

  constructor(codigo: string, message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.codigo = codigo;
    this.status = status;
  }
}

type UnauthorizedHandler = () => void;
let unauthorizedHandler: UnauthorizedHandler | null = null;

/** Chamado pelo `<AuthProvider>` — quando o refresh automático também falha, a sessão é
 * descartada e a UI volta pro login, sem cada tela precisar tratar isso individualmente. */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler;
}

async function parseErrorBody(res: Response): Promise<{ codigo: string; mensagem: string }> {
  try {
    const body = (await res.json()) as { codigo?: string; mensagem?: string };
    return { codigo: body.codigo ?? 'erro_desconhecido', mensagem: body.mensagem ?? `Erro HTTP ${res.status}` };
  } catch {
    return { codigo: 'erro_desconhecido', mensagem: `Erro HTTP ${res.status}` };
  }
}

/** Corpo cru de `TokensResponse` (.NET) — chega em camelCase (política padrão do
 * `System.Text.Json` nas Minimal APIs). */
interface TokensResponseBody {
  accessToken: string;
  refreshToken: string;
  expiraEm: string;
  usuario: UsuarioSessao;
}

function paraSessao(body: TokensResponseBody): Session {
  return { accessToken: body.accessToken, refreshToken: body.refreshToken, expiraEm: body.expiraEm, usuario: body.usuario };
}

/** Só um refresh em voo por vez — se três chamadas tomam 401 juntas (ex.: `Promise.all` de um
 * hook), as três esperam a MESMA promise em vez de gastar três refresh tokens rotativos (o
 * servidor invalida o antigo a cada rotação — um segundo refresh com o token já trocado falharia). */
let refreshEmVoo: Promise<Session | null> | null = null;

async function tentarRefresh(): Promise<Session | null> {
  const sessaoAtual = readSession();
  if (!sessaoAtual) return null;

  if (!refreshEmVoo) {
    refreshEmVoo = (async () => {
      try {
        const res = await fetch(`${BASE_URL}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: sessaoAtual.refreshToken }),
        });
        if (!res.ok) return null;
        const novaSessao = paraSessao((await res.json()) as TokensResponseBody);
        writeSession(novaSessao);
        return novaSessao;
      } catch {
        return null;
      } finally {
        refreshEmVoo = null;
      }
    })();
  }
  return refreshEmVoo;
}

async function request<T>(path: string, init: RequestInit = {}, jaTentouRefresh = false): Promise<T> {
  const session = readSession();
  const headers = new Headers(init.headers);
  if (init.body) headers.set('Content-Type', 'application/json');
  if (session) headers.set('Authorization', `Bearer ${session.accessToken}`);

  const res = await fetch(`${BASE_URL}/api${path}`, { ...init, headers });

  if (res.status === 401) {
    if (session && !jaTentouRefresh) {
      const novaSessao = await tentarRefresh();
      if (novaSessao) return request<T>(path, init, true);
    }
    writeSession(null);
    const { codigo, mensagem } = await parseErrorBody(res);
    unauthorizedHandler?.();
    throw new ApiError(codigo, mensagem, 401);
  }

  if (!res.ok) {
    const { codigo, mensagem } = await parseErrorBody(res);
    throw new ApiError(codigo, mensagem, res.status);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body !== undefined ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body !== undefined ? JSON.stringify(body) : undefined }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PATCH', body: body !== undefined ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};

/** `POST /api/auth/login` — troca e-mail+senha por um par access+refresh (`LoginUseCase` no
 * servidor). Sem boot-token: o financemax é acessível de qualquer lugar do mundo (§3 do prompt). */
export async function login(email: string, senha: string): Promise<Session> {
  const res = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, senha }),
  });

  if (!res.ok) {
    const { codigo, mensagem } = await parseErrorBody(res);
    throw new ApiError(codigo, mensagem, res.status);
  }

  const session = paraSessao((await res.json()) as TokensResponseBody);
  writeSession(session);
  return session;
}

/** Revoga o refresh token no servidor (best-effort — se a rede cair, a sessão local some do
 * mesmo jeito) e limpa a sessão local. */
export function logout(): void {
  const sessaoAtual = readSession();
  writeSession(null);
  if (sessaoAtual) {
    fetch(`${BASE_URL}/api/auth/logout`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: sessaoAtual.refreshToken }),
    }).catch(() => {
      // best-effort — a sessão local já foi limpa acima independentemente do resultado.
    });
  }
}
