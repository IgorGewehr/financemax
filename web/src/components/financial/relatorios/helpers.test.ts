import { describe, it, expect } from 'vitest';

import { agingWidths } from './helpers';
import type { AgingBucket } from './types';

function bucket(overrides: Partial<AgingBucket> = {}): AgingBucket {
  return { id: 'b1', label: '0-15d', amountCentavos: 1000, colorVar: 'var(--warn)', ...overrides };
}

describe('agingWidths', () => {
  it('larguras proporcionais ao valor de cada faixa, somando ~100', () => {
    const buckets = [bucket({ amountCentavos: 500 }), bucket({ amountCentavos: 500 })];
    const widths = agingWidths(buckets);
    expect(widths).toEqual([50, 50]);
    expect(widths.reduce((a, b) => a + b, 0)).toBeCloseTo(100, 5);
  });

  it('total zero retorna larguras zeradas, não NaN/Infinity', () => {
    const buckets = [bucket({ amountCentavos: 0 }), bucket({ amountCentavos: 0 })];
    expect(agingWidths(buckets)).toEqual([0, 0]);
  });

  it('lista vazia retorna lista vazia', () => {
    expect(agingWidths([])).toEqual([]);
  });

  it('nunca diverge do total real exibido — largura é sempre derivada, nunca hardcoded', () => {
    const buckets = [bucket({ amountCentavos: 300 }), bucket({ amountCentavos: 700 })];
    const total = buckets.reduce((s, b) => s + b.amountCentavos, 0);
    const widths = agingWidths(buckets);
    buckets.forEach((b, i) => {
      expect(widths[i]).toBeCloseTo((b.amountCentavos / total) * 100, 5);
    });
  });
});
