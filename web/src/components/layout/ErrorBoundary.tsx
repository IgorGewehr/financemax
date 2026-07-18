import { Component, type ReactNode } from 'react';

import { Button } from '@/components/ui/Button';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  error: Error | null;
}

/**
 * Rede de segurança de última instância — sem isto, QUALQUER exceção de render em qualquer tela
 * (ex.: dado real do backend fora do formato que o TS promete, ver
 * `lib/api/adapters/financeiro/entradasSaidas.ts`) derruba o app inteiro pra tela branca, porque
 * nenhum componente acima tem `componentDidCatch`. Fica em `AppShell` (ver `App.tsx`), com `key`
 * de rota — assim uma tela que quebrou não trava as outras: navegar pra longe e voltar remonta.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: { componentStack: string }) {
    console.error('[ErrorBoundary] tela quebrou:', error, info.componentStack);
  }

  render() {
    if (this.state.error) {
      return (
        <div className="flex min-h-[50vh] w-full flex-col items-center justify-center gap-3 p-6 text-center">
          <h2 className="font-display text-lg font-bold text-foreground">Essa tela quebrou</h2>
          <p className="max-w-sm text-sm text-muted-foreground">
            Algo inesperado aconteceu ao montar esta página. Tente recarregar — se persistir, avise o suporte.
          </p>
          <Button variant="primary" size="sm" onClick={() => window.location.reload()}>
            Recarregar
          </Button>
        </div>
      );
    }
    return this.props.children;
  }
}
