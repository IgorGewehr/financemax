import { motion } from 'framer-motion';
import { Mail, ShieldCheck, UserPlus } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/ui/EmptyState';
import { Input } from '@/components/ui/Input';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { ApiError } from '@/lib/api/client';
import { onboardingApi, type ValidarConviteDto } from '@/lib/api/onboarding';
import { useAuth } from '@/lib/auth';
import { useToast } from '@/lib/toast';

const PAPEL_LABEL: Record<string, string> = {
  founder: 'Dono',
  admin: 'Administrador',
  manager: 'Gerente',
  operator: 'Operador',
  viewer: 'Visualizador',
};

function papelLabel(papel: string): string {
  return PAPEL_LABEL[papel] ?? papel;
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível criar a conta. Tente novamente.';
}

interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

/**
 * `/criar-conta` — onboarding web (F3, contrato acordado com o backend): com `?token=xxx` aceita
 * um convite (`GET /api/convites/{token}` pré-preenche e-mail/papel, só leitura); sem token abre
 * o formulário de criação do 1º dono — o próprio `POST /api/auth/registrar` decide no servidor se
 * vira founder (nenhum usuário ainda) ou exige convite (mensagem de erro guia o usuário nesse
 * caso, sem a SPA precisar de um endpoint dedicado de "é o primeiro usuário?"). Sucesso já vem com
 * a sessão pronta (`onboardingApi.registrar` escreve no `localStorage`); `applySession` sincroniza
 * o `AuthProvider` sem precisar de reload.
 */
export function CriarConta() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const navigate = useNavigate();
  const { applySession } = useAuth();
  const { toast } = useToast();

  const [convite, setConvite] = useState<Recurso<ValidarConviteDto>>({ dado: null, erro: null, carregando: Boolean(token) });
  const [nome, setNome] = useState('');
  const [senha, setSenha] = useState('');
  const [confirmarSenha, setConfirmarSenha] = useState('');
  const [emailFounder, setEmailFounder] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!token) return;
    let cancelado = false;
    setConvite({ dado: null, erro: null, carregando: true });
    onboardingApi
      .validarConvite(token)
      .then((dto) => {
        if (!cancelado) setConvite({ dado: dto, erro: null, carregando: false });
      })
      .catch((e: unknown) => {
        if (!cancelado) setConvite({ dado: null, erro: mensagemDeErro(e), carregando: false });
      });
    return () => {
      cancelado = true;
    };
  }, [token]);

  const emailConvite = convite.dado?.email ?? null;
  const conviteInvalido = Boolean(token) && !convite.carregando && (convite.erro || !convite.dado || convite.dado.valido === false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (enviando) return;

    const email = token ? (emailConvite ?? '') : emailFounder.trim();
    if (!nome.trim() || !email || !senha) return;
    if (senha !== confirmarSenha) {
      setErro('As senhas não coincidem.');
      return;
    }
    if (senha.length < 8) {
      setErro('A senha precisa ter pelo menos 8 caracteres.');
      return;
    }

    setErro(null);
    setEnviando(true);
    try {
      const session = await onboardingApi.registrar({
        nome: nome.trim(),
        email,
        senha,
        ...(token ? { conviteToken: token } : {}),
      });
      applySession(session);
      toast(`Bem-vindo(a), ${session.usuario.nome.split(' ')[0]}!`, 'success');
      navigate('/', { replace: true });
    } catch (e) {
      setErro(mensagemDeErro(e));
      setSenha('');
      setConfirmarSenha('');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="flex min-h-dvh w-full items-center justify-center bg-background px-4 py-8">
      <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }} className="w-full max-w-sm">
        <Surface rounded="2xl" padding="lg" className="flex flex-col items-center gap-5 text-center">
          <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-red text-xl font-bold text-white shadow-red">
            FX
          </span>

          {token && convite.carregando ? (
            <div className="w-full space-y-2">
              <Skeleton className="mx-auto h-5 w-40" />
              <Skeleton className="mx-auto h-3 w-56" />
              <Skeleton className="mt-4 h-10 w-full" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : conviteInvalido ? (
            <EmptyState
              icon={<ShieldCheck className="h-5 w-5" />}
              title="Este convite não é mais válido"
              description={convite.erro ?? 'O link pode ter expirado ou já ter sido usado. Peça um novo convite a quem administra o financemax.'}
              className="border-none py-2"
            />
          ) : (
            <>
              <div>
                <h1 className="font-display text-xl font-bold text-foreground">
                  {token ? 'Aceitar convite' : 'Criar sua conta'}
                </h1>
                <p className="mt-1 text-sm text-muted-foreground">
                  {token
                    ? `Você foi convidado(a) como ${papelLabel(convite.dado?.papel ?? '')}.`
                    : 'Primeiro acesso ao financemax — esta conta vira a dona do sistema.'}
                </p>
              </div>

              <form onSubmit={onSubmit} className="flex w-full flex-col gap-3.5 text-left">
                {token ? (
                  <div>
                    <label className="mb-1.5 block text-xs font-semibold text-muted-foreground">E-mail</label>
                    <div className="relative">
                      <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/60" />
                      <Input value={emailConvite ?? ''} readOnly disabled className="pl-9" />
                    </div>
                  </div>
                ) : (
                  <div>
                    <label htmlFor="email-founder" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
                      E-mail
                    </label>
                    <div className="relative">
                      <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/60" />
                      <Input
                        id="email-founder"
                        type="email"
                        autoComplete="email"
                        value={emailFounder}
                        onChange={(e) => {
                          setErro(null);
                          setEmailFounder(e.target.value);
                        }}
                        placeholder="voce@empresa.com"
                        className="pl-9"
                      />
                    </div>
                  </div>
                )}

                <div>
                  <label htmlFor="nome" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
                    Nome
                  </label>
                  <Input
                    id="nome"
                    autoFocus={Boolean(token)}
                    autoComplete="name"
                    value={nome}
                    onChange={(e) => {
                      setErro(null);
                      setNome(e.target.value);
                    }}
                    placeholder="Seu nome"
                  />
                </div>

                <div>
                  <label htmlFor="senha" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
                    Senha
                  </label>
                  <Input
                    id="senha"
                    type="password"
                    autoComplete="new-password"
                    value={senha}
                    onChange={(e) => {
                      setErro(null);
                      setSenha(e.target.value);
                    }}
                    placeholder="Mínimo 8 caracteres"
                  />
                </div>

                <div>
                  <label htmlFor="confirmar-senha" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
                    Confirmar senha
                  </label>
                  <Input
                    id="confirmar-senha"
                    type="password"
                    autoComplete="new-password"
                    value={confirmarSenha}
                    onChange={(e) => {
                      setErro(null);
                      setConfirmarSenha(e.target.value);
                    }}
                    placeholder="Repita a senha"
                  />
                </div>

                {erro && (
                  <motion.p initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="text-center text-sm font-medium text-red-600 dark:text-red-400">
                    {erro}
                  </motion.p>
                )}

                <Button
                  type="submit"
                  variant="primary"
                  size="touch"
                  className="mt-1.5 w-full"
                  disabled={enviando || !nome || !senha || !confirmarSenha || (token ? !emailConvite : !emailFounder)}
                  icon={<UserPlus className="h-4 w-4" />}
                >
                  {enviando ? 'Criando…' : token ? 'Aceitar convite e entrar' : 'Criar conta e entrar'}
                </Button>
              </form>

              <button
                type="button"
                onClick={() => navigate('/', { replace: true })}
                className="text-xs font-semibold text-muted-foreground hover:text-foreground"
              >
                Já tenho conta — voltar para o login
              </button>
            </>
          )}
        </Surface>
      </motion.div>
    </div>
  );
}
