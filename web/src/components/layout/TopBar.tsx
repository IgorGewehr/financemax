import { AnimatePresence, motion } from 'framer-motion';
import { LogOut, Moon, Search, Sun } from 'lucide-react';

import { useAuth } from '@/lib/auth';
import { useTheme } from '@/lib/theme';

function ThemeToggle() {
  const { isDark, toggleDark } = useTheme();
  return (
    <button
      type="button"
      onClick={toggleDark}
      className="relative flex h-9 w-9 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
      title={isDark ? 'Modo claro' : 'Modo escuro'}
    >
      <AnimatePresence mode="wait" initial={false}>
        {isDark ? (
          <motion.span key="moon" initial={{ rotate: -90, scale: 0, opacity: 0 }} animate={{ rotate: 0, scale: 1, opacity: 1 }} exit={{ rotate: 90, scale: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <Moon className="h-[17px] w-[17px]" />
          </motion.span>
        ) : (
          <motion.span key="sun" initial={{ rotate: 90, scale: 0, opacity: 0 }} animate={{ rotate: 0, scale: 1, opacity: 1 }} exit={{ rotate: -90, scale: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <Sun className="h-[17px] w-[17px]" />
          </motion.span>
        )}
      </AnimatePresence>
    </button>
  );
}

function iniciaisDe(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '?';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return `${partes[0][0]}${partes[partes.length - 1][0]}`.toUpperCase();
}

/**
 * Barra superior única do app financeiro-only — sem hambúrguer/`Sidebar` (não existe outro módulo
 * pra navegar pra fora do Financeiro, ver `AppShell`). Avatar mostra as iniciais do `session.usuario`
 * autenticado por e-mail/senha (ver `lib/auth.tsx`), não mais um nome fixo.
 */
export function TopBar() {
  const { logout, session } = useAuth();
  const usuario = session?.usuario;

  return (
    <header className="flex h-16 shrink-0 items-center gap-3 border-b border-border/70 bg-background/80 px-4 backdrop-blur-md sm:px-6">
      <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-red text-xs font-bold text-white shadow-red">
        FX
      </span>

      <div className="relative hidden max-w-xs flex-1 items-center sm:flex">
        <Search className="pointer-events-none absolute left-3 h-4 w-4 text-muted-foreground/60" />
        <input
          placeholder="Buscar em Financeiro…"
          className="w-full rounded-xl border border-transparent bg-secondary/70 py-2 pl-9 pr-3 text-sm text-foreground outline-none placeholder:text-muted-foreground/60 focus:border-border focus:bg-background"
        />
      </div>

      <div className="ml-auto flex items-center gap-2">
        <ThemeToggle />
        <button
          type="button"
          onClick={logout}
          title="Sair"
          className="flex h-9 w-9 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
        >
          <LogOut className="h-[17px] w-[17px]" />
        </button>
        {usuario && (
          <div
            className="ml-1 flex h-9 w-9 items-center justify-center rounded-full bg-gradient-red text-xs font-bold text-white"
            title={`${usuario.nome} · ${usuario.email}`}
          >
            {iniciaisDe(usuario.nome)}
          </div>
        )}
      </div>
    </header>
  );
}
