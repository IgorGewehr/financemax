import { motion } from 'framer-motion';
import { Eye, EyeOff, LogIn, Mail } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';

import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Surface } from '@/components/ui/Surface';
import { ApiError, useAuth } from '@/lib/auth';

/**
 * Tela de login — primeira coisa que a SPA mostra sem sessão válida (ver `AuthProvider`/
 * `AuthGate`). E-mail + senha contra `POST /api/auth/login` (§3 do prompt da F3): substitui a
 * tela de PIN local do sistemax de origem — o financemax é acessado de qualquer lugar do mundo,
 * não só da máquina que roda o host desktop. Tema claro por construção (ver `lib/theme.tsx`).
 */
export function Login() {
  const { login, loading } = useAuth();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [mostrarSenha, setMostrarSenha] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!email || !senha || loading) return;
    setErro(null);
    try {
      await login(email.trim(), senha);
    } catch (err) {
      const mensagem = err instanceof ApiError ? err.message : 'Não foi possível entrar. Tente novamente.';
      setErro(mensagem);
      setSenha('');
    }
  }

  return (
    <div className="flex min-h-dvh w-full items-center justify-center bg-background px-4">
      <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }} className="w-full max-w-sm">
        <Surface rounded="2xl" padding="lg" className="flex flex-col items-center gap-5 text-center">
          <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-red text-xl font-bold text-white shadow-red">
            FX
          </span>
          <div>
            <h1 className="font-display text-xl font-bold text-foreground">financemax</h1>
            <p className="mt-1 text-sm text-muted-foreground">Entre com e-mail e senha para continuar</p>
          </div>

          <form onSubmit={onSubmit} className="flex w-full flex-col gap-3.5 text-left">
            <div>
              <label htmlFor="email" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
                E-mail
              </label>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground/60" />
                <Input
                  id="email"
                  type="email"
                  autoComplete="email"
                  autoFocus
                  value={email}
                  onChange={(e) => {
                    setErro(null);
                    setEmail(e.target.value);
                  }}
                  placeholder="voce@empresa.com"
                  className="pl-9"
                />
              </div>
            </div>

            <div>
              <label htmlFor="senha" className="mb-1.5 block text-xs font-semibold text-muted-foreground">
                Senha
              </label>
              <div className="relative">
                <Input
                  id="senha"
                  type={mostrarSenha ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={senha}
                  onChange={(e) => {
                    setErro(null);
                    setSenha(e.target.value);
                  }}
                  placeholder="••••••••"
                  className="pr-9"
                />
                <button
                  type="button"
                  onClick={() => setMostrarSenha((v) => !v)}
                  aria-label={mostrarSenha ? 'Ocultar senha' : 'Mostrar senha'}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground/70 hover:text-foreground"
                >
                  {mostrarSenha ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            {erro && (
              <motion.p initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="text-center text-sm font-medium text-red-600 dark:text-red-400">
                {erro}
              </motion.p>
            )}

            <Button type="submit" variant="primary" size="touch" className="mt-1.5 w-full" disabled={!email || !senha || loading} icon={<LogIn className="h-4 w-4" />}>
              {loading ? 'Entrando…' : 'Entrar'}
            </Button>
          </form>

          <Link to="/criar-conta" className="text-xs font-semibold text-muted-foreground hover:text-foreground">
            Primeiro acesso ou recebeu um convite? Criar conta
          </Link>
        </Surface>
      </motion.div>
    </div>
  );
}
