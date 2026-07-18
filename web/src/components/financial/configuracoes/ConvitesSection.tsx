import { Check, Copy, Mail, Trash2, UserPlus } from 'lucide-react';
import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { formatDateTime } from '@/lib/format';
import { useToast } from '@/lib/toast';

import { useConvites } from './useConvites';

const PAPEL_OPCOES = [
  { value: 'admin', label: 'Administrador' },
  { value: 'manager', label: 'Gerente' },
  { value: 'operator', label: 'Operador' },
  { value: 'viewer', label: 'Visualizador' },
];

function papelLabel(papel: string): string {
  return PAPEL_OPCOES.find((p) => p.value === papel)?.label ?? papel;
}

function linkDoConvite(token: string): string {
  return `${window.location.origin}/criar-conta?token=${encodeURIComponent(token)}`;
}

/** Bloco "Convidar" de Configurações — só visível a founder/admin (checado em `Configuracoes.tsx`).
 * `POST /api/convites` gera o token; o link pronto (`/criar-conta?token=...`) é a única forma de
 * distribuir o convite hoje (sem envio de e-mail automático no contrato v1). */
export function ConvitesSection() {
  const vm = useConvites();
  const { toast } = useToast();
  const [email, setEmail] = useState('');
  const [papel, setPapel] = useState(PAPEL_OPCOES[0].value);
  const [linkGerado, setLinkGerado] = useState<string | null>(null);
  const [copiado, setCopiado] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!email.trim() || vm.criando) return;
    try {
      const convite = await vm.criar({ email: email.trim(), papel });
      setLinkGerado(linkDoConvite(convite.token));
      setCopiado(false);
      setEmail('');
      toast(`Convite criado para ${convite.email}.`, 'success');
    } catch {
      toast('Não foi possível criar o convite. Tente novamente.', 'warning');
    }
  }

  async function copiarLink(link: string) {
    try {
      await navigator.clipboard.writeText(link);
      setCopiado(true);
      toast('Link copiado ✓', 'success');
      window.setTimeout(() => setCopiado(false), 2000);
    } catch {
      toast('Não deu para copiar automaticamente — selecione e copie manualmente.', 'warning');
    }
  }

  async function onRevogar(id: string, emailConvite: string) {
    try {
      await vm.revogar(id);
      toast(`Convite de ${emailConvite} revogado.`, 'success');
    } catch {
      toast('Não foi possível revogar este convite agora.', 'warning');
    }
  }

  return (
    <div className="rounded-2xl border border-border bg-card px-4 py-4 sm:px-5">
      <div className="flex items-center gap-2">
        <UserPlus className="h-4 w-4 text-primary-600" />
        <h3 className="text-sm font-bold text-foreground">Convidar</h3>
      </div>
      <p className="mt-1 text-[13px] leading-relaxed text-muted-foreground">
        Convide alguém do time para acessar o financemax. A pessoa recebe um link para criar a própria conta.
      </p>

      <form onSubmit={onSubmit} className="mt-3.5 flex flex-col gap-2.5 sm:flex-row sm:items-end">
        <div className="flex-1">
          <label htmlFor="convite-email" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            E-mail
          </label>
          <div className="relative">
            <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/60" />
            <Input
              id="convite-email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="colega@empresa.com"
              className="pl-9"
            />
          </div>
        </div>

        <div>
          <label htmlFor="convite-papel" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
            Papel
          </label>
          <select
            id="convite-papel"
            value={papel}
            onChange={(e) => setPapel(e.target.value)}
            className="h-[42px] rounded-xl border border-border bg-surface-2 px-3 text-sm text-foreground outline-none focus:ring-2 focus:ring-ring"
          >
            {PAPEL_OPCOES.map((op) => (
              <option key={op.value} value={op.value}>
                {op.label}
              </option>
            ))}
          </select>
        </div>

        <Button type="submit" variant="primary" size="md" disabled={!email.trim() || vm.criando}>
          {vm.criando ? 'Gerando…' : 'Gerar convite'}
        </Button>
      </form>

      {linkGerado && (
        <div className="mt-3 flex items-center gap-2 rounded-xl border border-primary-600/30 bg-primary-soft px-3 py-2.5">
          <code className="min-w-0 flex-1 truncate text-[12.5px] font-medium text-foreground">{linkGerado}</code>
          <button
            type="button"
            onClick={() => copiarLink(linkGerado)}
            className="flex shrink-0 items-center gap-1.5 rounded-lg border border-primary-600/35 px-2.5 py-1 text-[11.5px] font-bold text-primary-600 hover:bg-primary-600/10"
          >
            {copiado ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
            {copiado ? 'Copiado' : 'Copiar'}
          </button>
        </div>
      )}

      {!vm.convites.carregando && vm.convites.dado && vm.convites.dado.length > 0 && (
        <div className="mt-4 flex flex-col gap-2">
          <h4 className="text-xs font-semibold text-muted-foreground">Convites pendentes</h4>
          {/* Sem botão "Copiar" aqui de propósito — o servidor só guarda o hash do token, então o
           * link só existe uma vez, na resposta de criação (bloco `linkGerado` acima). Convites
           * recarregados (reload de página, outra sessão) só podem ser revogados. */}
          {vm.convites.dado.map((c) => (
            <div key={c.id} className="flex items-center justify-between gap-2.5 rounded-xl bg-surface-2 px-3 py-2.5">
              <div className="min-w-0">
                <p className="truncate text-[13px] font-semibold text-foreground">{c.email}</p>
                <p className="text-[11.5px] text-muted-foreground">
                  {papelLabel(c.papel)} · expira {formatDateTime(c.expiraEm)}
                </p>
              </div>
              <div className="flex shrink-0 items-center gap-1.5">
                <button
                  type="button"
                  disabled={vm.revogandoId === c.id}
                  onClick={() => onRevogar(c.id, c.email)}
                  aria-label="Revogar convite"
                  title="Revogar convite"
                  className="flex h-7 w-7 items-center justify-center rounded-lg text-crit hover:bg-crit-soft disabled:opacity-50"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
