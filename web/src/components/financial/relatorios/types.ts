import type { Centavos } from '@/lib/money';

/**
 * View-model da tela Relatórios — todo campo abaixo é dado REAL (`useRelatoriosReais.ts`). A
 * geração de PDF/Excel, o pacote de fechamento (.zip), o envio por e-mail/WhatsApp, o regime de
 * caixa do DRE e o histórico de exports saíram da tela: nenhum tinha serviço/persistência real no
 * backend (eram só simulação de UI, ver `docs/wiring/financeiro-telas-restantes.md §5`).
 */

/**
 * Trecho de texto com negrito opcional — usado na nota de bridge do DRE pra preservar o `<b>` sem
 * colocar JSX dentro do adapter (`.ts` puro).
 */
export interface RichTextPart {
  text: string;
  bold?: boolean;
}

export interface DreLine {
  label: string;
  valueCentavos: Centavos;
}

/** DRE gerencial, regime de COMPETÊNCIA (único real — regime de caixa não tem serviço no backend). */
export interface DreRegimeBlock {
  /** Rótulo curto exibido no `doc-sub` do card ("competência"). */
  regimeLabel: string;
  topLine: DreLine;
  /** Linhas de dedução, na ordem exibida (estilo `.v.neg` — mais claras, nunca vermelhas). */
  deductionLines: DreLine[];
  totalLine: DreLine;
  delta: {
    direction: 'up' | 'down';
    label: string;
  };
  bridgeNote: RichTextPart[];
}

export interface AgingBucket {
  id: string;
  label: string;
  amountCentavos: Centavos;
  /**
   * Valor de cor via `var(--token)` (não classe Tailwind) — a faixa de 0–15d usa opacidade parcial
   * do `warn`, e o modificador `bg-warn/NN` do Tailwind não é garantido pra um token sem
   * `<alpha-value>` no config; referenciar a CSS var diretamente reproduz o mockup com segurança.
   */
  colorVar: string;
}

export interface AbertoViewModel {
  receberEmAberto: Centavos;
  receberAtrasado: Centavos;
  pagarEmAberto: Centavos;
  agingBuckets: AgingBucket[];
}

export interface MrrViewModel {
  condicaoLabel: string;
  mrr: Centavos;
  churnMes: Centavos;
  arrEstimado: Centavos;
}
