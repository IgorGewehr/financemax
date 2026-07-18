import { describe, it, expect } from 'vitest';

import { atrasados30MaisDias, buildTimeline, insertLancamentoOrdenado, TODAY_ISO } from './calc';
import type { LancamentoRow } from './types';

function lancamento(overrides: Partial<LancamentoRow> = {}): LancamentoRow {
  return {
    id: 'r1',
    data: '2026-07-01',
    desc: 'Aluguel',
    sub: null,
    categoria: 'aluguel',
    tipo: 'saida',
    status: 'pago',
    valorCentavos: 100_000,
    ...overrides,
  };
}

describe('atrasados30MaisDias', () => {
  it('filtra só status atrasado com diasAtraso > 30, soma valor e conta', () => {
    const rows = [
      lancamento({ id: 'a', status: 'atrasado', diasAtraso: 31, valorCentavos: 1000 }),
      lancamento({ id: 'b', status: 'atrasado', diasAtraso: 30, valorCentavos: 2000 }), // exatamente 30, não conta
      lancamento({ id: 'c', status: 'atrasado', diasAtraso: 45, valorCentavos: 500 }),
      lancamento({ id: 'd', status: 'pago', diasAtraso: 60, valorCentavos: 999 }),
    ];
    const r = atrasados30MaisDias(rows);
    expect(r.qtdClientes).toBe(2);
    expect(r.totalCentavos).toBe(1500);
  });

  it('sem diasAtraso definido (undefined) trata como 0 — não quebra', () => {
    const rows = [lancamento({ status: 'atrasado', diasAtraso: undefined })];
    expect(atrasados30MaisDias(rows).qtdClientes).toBe(0);
  });

  it('lista vazia produz zeros', () => {
    const r = atrasados30MaisDias([]);
    expect(r.totalCentavos).toBe(0);
    expect(r.qtdClientes).toBe(0);
  });
});

describe('buildTimeline', () => {
  it('insere divisor "Hoje" antes do 1º lançamento já vencido/realizado', () => {
    const rows = [
      lancamento({ id: 'futuro', data: '2026-07-20' }),
      lancamento({ id: 'hoje-ou-antes', data: TODAY_ISO }),
      lancamento({ id: 'passado', data: '2026-07-01' }),
    ];
    const entries = buildTimeline(rows, 'tudo', null);
    const kinds = entries.map((e) => e.kind);
    expect(kinds).toEqual(['row', 'divider', 'row', 'row']);
  });

  it('sem nenhum lançamento vencido, não insere divisor', () => {
    const rows = [lancamento({ data: '2026-08-01' })];
    const entries = buildTimeline(rows, 'tudo', null);
    expect(entries.some((e) => e.kind === 'divider')).toBe(false);
  });

  it('segFiltro "receber" mantém só entradas; "pagar" só saídas', () => {
    const rows = [
      lancamento({ id: 'in', tipo: 'entrada', data: '2026-08-01' }),
      lancamento({ id: 'out', tipo: 'saida', data: '2026-08-02' }),
    ];
    const receber = buildTimeline(rows, 'receber', null).filter((e) => e.kind === 'row');
    expect(receber).toHaveLength(1);
    const pagar = buildTimeline(rows, 'pagar', null).filter((e) => e.kind === 'row');
    expect(pagar).toHaveLength(1);
  });

  it('filtro por categoria mantém só as linhas com a categoria filtrada', () => {
    const rows = [
      lancamento({ id: 'a', categoria: 'cmv-fornecedor', data: '2026-08-01' }),
      lancamento({ id: 'b', categoria: 'aluguel', data: '2026-08-02' }),
    ];
    const filtradas = buildTimeline(rows, 'tudo', { type: 'categoria', value: 'cmv-fornecedor', label: 'Fornecedores' }).filter(
      (e) => e.kind === 'row',
    );
    expect(filtradas).toHaveLength(1);
  });
});

describe('insertLancamentoOrdenado', () => {
  it('insere mantendo ordem decrescente por data (mais recente primeiro)', () => {
    const rows = [lancamento({ id: 'a', data: '2026-08-01' }), lancamento({ id: 'c', data: '2026-07-01' })];
    const novo = lancamento({ id: 'b', data: '2026-07-15' });
    const result = insertLancamentoOrdenado(rows, novo);
    expect(result.map((r) => r.id)).toEqual(['a', 'b', 'c']);
  });

  it('data mais recente que tudo vai pro início', () => {
    const rows = [lancamento({ id: 'a', data: '2026-07-01' })];
    const novo = lancamento({ id: 'novo', data: '2026-08-01' });
    expect(insertLancamentoOrdenado(rows, novo).map((r) => r.id)).toEqual(['novo', 'a']);
  });

  it('data mais antiga que tudo vai pro fim (índice -1 -> append)', () => {
    const rows = [lancamento({ id: 'a', data: '2026-08-01' })];
    const novo = lancamento({ id: 'novo', data: '2026-01-01' });
    expect(insertLancamentoOrdenado(rows, novo).map((r) => r.id)).toEqual(['a', 'novo']);
  });
});
