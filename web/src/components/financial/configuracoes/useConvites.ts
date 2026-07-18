import { useCallback, useEffect, useState } from 'react';

import { ApiError } from '@/lib/api/client';
import {
  onboardingApi,
  type ConviteDto,
  type ConviteEmitidoDto,
  type CriarConviteRequest,
} from '@/lib/api/onboarding';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function inicial<T>(): Recurso<T> {
  return { dado: null, erro: null, carregando: true };
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar.';
}

/**
 * Convites pendentes (`GET/POST /api/convites`, `POST /api/convites/{id}/revogar`) — vive em
 * Configurações, só visível a founder/admin (checado no componente, ver `ConvitesSection.tsx`).
 * `criar` devolve o `ConviteEmitidoDto` (com o token bruto, que só existe nessa resposta) e
 * DEPOIS recarrega a lista do servidor — não dá pra empurrar o item retornado direto pra lista
 * local porque `ConviteDto` (o shape de `GET /convites`) tem `id` e não tem `token`; recarregar é
 * o único jeito de obter o `id` que o revogar precisa. `revogar` é por `id`, nunca por token.
 */
export function useConvites() {
  const [convites, setConvites] = useState<Recurso<ConviteDto[]>>(inicial);
  const [criando, setCriando] = useState(false);
  const [revogandoId, setRevogandoId] = useState<string | null>(null);

  const carregar = useCallback(() => {
    setConvites(inicial());
    onboardingApi
      .listarConvites()
      .then((dto) => setConvites({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setConvites({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  const criar = useCallback(
    async (payload: CriarConviteRequest): Promise<ConviteEmitidoDto> => {
      setCriando(true);
      try {
        const novo = await onboardingApi.criarConvite(payload);
        carregar();
        return novo;
      } finally {
        setCriando(false);
      }
    },
    [carregar],
  );

  const revogar = useCallback(async (id: string) => {
    setRevogandoId(id);
    try {
      await onboardingApi.revogarConvite(id);
      setConvites((prev) => ({ ...prev, dado: (prev.dado ?? []).filter((c) => c.id !== id) }));
    } finally {
      setRevogandoId(null);
    }
  }, []);

  return { convites, criando, criar, revogandoId, revogar, recarregar: carregar };
}

export type ConvitesVm = ReturnType<typeof useConvites>;
