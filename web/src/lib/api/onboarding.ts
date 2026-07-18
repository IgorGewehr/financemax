/**
 * Cliente de onboarding — contrato FECHADO com o backend (ver `IdentidadeEndpointsModule.cs`):
 * `POST /api/auth/registrar` {nome,email,senha,conviteToken?} cria conta (1º usuário sem token =
 * founder; senão exige `conviteToken` válido) e devolve sessão pronta (mesmo shape de `Session`
 * de `client.ts` — `accessToken`/`refreshToken`/`expiraEm`/`usuario`), então `registrar()` já
 * escreve a sessão local, igual `login()`.
 *
 * Convites — DOIS shapes distintos, de propósito (o token bruto só existe uma vez, na criação):
 * - `POST /convites` devolve `ConviteEmitidoDto` (COM `token`) — única resposta HTTP que carrega o
 *   token bruto; é o que `ConvitesSection.tsx` usa pra montar o link "Copiar" exibido na hora.
 * - `GET /convites` devolve `ConviteDto[]` (SEM token, COM `id`) — o servidor só guarda o HASH do
 *   token, nunca o valor bruto, então não há como devolvê-lo de novo depois da criação. Convites
 *   pré-existentes (de sessões/reloads anteriores) não têm link "Copiar" disponível — só
 *   revogar (`POST /convites/{id}/revogar`, por `id`, não por token).
 * - `GET /convites/{token}` é ANÔNIMO (sem sessão local — usado pela tela `/criar-conta?token=...`
 *   antes do usuário existir, pra pré-validar o convite).
 */
import { api, writeSession, type Session } from './client';

export interface RegistrarContaRequest {
  nome: string;
  email: string;
  senha: string;
  /** Presente só no fluxo "aceitar convite" — ausente no fluxo "criar 1º dono" (first-run). */
  conviteToken?: string;
}

export interface CriarConviteRequest {
  email: string;
  papel: string;
}

/** Resposta de `POST /convites` — espelha `ConviteEmitidoResponse` (.NET). Único lugar onde o
 * token bruto aparece; não tem `id` (o servidor não devolve o Id nessa resposta). */
export interface ConviteEmitidoDto {
  token: string;
  email: string;
  papel: string;
  expiraEm: string;
}

/** Item de `GET /convites` — espelha `ConviteDto` (.NET). SEM token (nunca sai de novo depois da
 * criação); `id` é a chave usada pra revogar. */
export interface ConviteDto {
  id: string;
  email: string;
  papel: string;
  criadoEm: string;
  expiraEm: string;
}

export interface ValidarConviteDto {
  email: string;
  papel: string;
  valido: boolean;
  motivo?: string | null;
}

export const onboardingApi = {
  registrar: async (payload: RegistrarContaRequest): Promise<Session> => {
    const session = await api.post<Session>('/auth/registrar', payload);
    writeSession(session);
    return session;
  },
  criarConvite: (payload: CriarConviteRequest) => api.post<ConviteEmitidoDto>('/convites', payload),
  listarConvites: () => api.get<ConviteDto[]>('/convites'),
  /** Anônimo por design (usuário ainda não existe) — `client.ts` só anexa `Authorization` quando
   * há sessão local; sem sessão (caso comum aqui), a chamada já sai sem header. */
  validarConvite: (token: string) => api.get<ValidarConviteDto>(`/convites/${encodeURIComponent(token)}`),
  /** `POST /convites/{id}/revogar` (não DELETE, não por token — ver `IdentidadeEndpointsModule.cs`). */
  revogarConvite: (id: string) => api.post<void>(`/convites/${encodeURIComponent(id)}/revogar`),
};
