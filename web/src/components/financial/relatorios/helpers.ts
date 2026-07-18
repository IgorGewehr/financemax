import type { AgingBucket } from './types';

/**
 * Larguras (%) da barra de aging — derivadas dos valores das faixas, nunca hardcoded, pra nunca
 * divergir do total exibido no flag "atrasado" (replica visualmente o mockup sem duplicar números).
 */
export function agingWidths(buckets: AgingBucket[]): number[] {
  const total = buckets.reduce((sum, b) => sum + b.amountCentavos, 0);
  if (total <= 0) return buckets.map(() => 0);
  return buckets.map((b) => (b.amountCentavos / total) * 100);
}
