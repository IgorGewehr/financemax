import { todayIso } from '@/lib/date';
import { formatDateShort } from '@/lib/format';

import type { Atrasados30DiasResumo, CategoriaId, CategoriasLancamentoRapido, FiltroAtivo, LancamentoRow, SegFiltro, TimelineEntry } from './types';

/** "Hoje" real (relógio de parede) — a Linha do tempo vem do backend real (`GET /financeiro/extrato`). */
export const TODAY_ISO = todayIso();

export const CATEGORIA_LABEL: Record<CategoriaId, string> = {
  folha: 'Folha',
  fornecedores: 'Fornecedores',
  aluguel: 'Aluguel',
  impostos: 'Impostos',
  software: 'Software',
  marketing: 'Marketing',
  servicos: 'Serviços',
};

/** Categorias oferecidas no formulário de "Lançamento rápido" — config estática do app, não dado
 * financeiro (nenhum valor numérico aqui, só rótulos do form). */
export const CATEGORIAS_LANCAMENTO_RAPIDO: CategoriasLancamentoRapido = {
  entrada: ['Serviços', 'Produtos', 'Outra receita'],
  saida: ['Folha', 'Fornecedores', 'Aluguel', 'Impostos', 'Software', 'Marketing', 'Outra despesa'],
};

/** Mapa categoria (label do form) → `CategoriaId` — mesmo `catMap` do lançamento rápido do mockup. */
export const CATEGORIA_MAP_LANCAMENTO_RAPIDO: Record<string, CategoriaId> = {
  Serviços: 'servicos',
  Produtos: 'servicos',
  'Outra receita': 'servicos',
  Folha: 'folha',
  Fornecedores: 'fornecedores',
  Aluguel: 'aluguel',
  Impostos: 'impostos',
  Software: 'software',
  Marketing: 'marketing',
  'Outra despesa': 'marketing',
};

/** "cmv-fornecedor" → "Cmv Fornecedor" — fallback de rótulo pra `categoriaId` que o domínio real
 * devolve (`GET /financeiro/extrato`) e não está no catálogo conhecido de `CATEGORIA_LABEL`. Nunca
 * indexa `CATEGORIA_LABEL` à força com um `string` livre. */
export function categoriaLabel(id: string): string {
  const conhecida = CATEGORIA_LABEL[id as CategoriaId];
  if (conhecida) return conhecida;
  return id
    .split(/[-_]/)
    .filter(Boolean)
    .map((parte) => parte.charAt(0).toUpperCase() + parte.slice(1))
    .join(' ');
}

/** Recebíveis atrasados há mais de 30 dias — dado real, derivado da Linha do tempo. */
export function atrasados30MaisDias(rows: LancamentoRow[]): Atrasados30DiasResumo {
  const atrasados = rows.filter((r) => r.status === 'atrasado' && (r.diasAtraso ?? 0) > 30);
  return {
    totalCentavos: atrasados.reduce((a, r) => a + r.valorCentavos, 0),
    qtdClientes: atrasados.length,
  };
}

function passaFiltro(row: LancamentoRow, segFiltro: SegFiltro, filtro: FiltroAtivo | null): boolean {
  if (segFiltro === 'receber' && row.tipo !== 'entrada') return false;
  if (segFiltro === 'pagar' && row.tipo !== 'saida') return false;
  if (filtro) {
    if (filtro.type === 'categoria' && row.categoria !== filtro.value) return false;
    if (filtro.type === 'status' && row.status !== 'atrasado') return false;
  }
  return true;
}

/** Monta a Linha do tempo: divisor "Hoje" antes do 1º lançamento já vencido/realizado. */
export function buildTimeline(rows: LancamentoRow[], segFiltro: SegFiltro, filtro: FiltroAtivo | null): TimelineEntry[] {
  const entries: TimelineEntry[] = [];
  let dividerInserido = false;
  for (const row of rows) {
    if (!dividerInserido && row.data <= TODAY_ISO) {
      entries.push({ kind: 'divider', label: `Hoje · ${formatDateShort(TODAY_ISO)}` });
      dividerInserido = true;
    }
    if (passaFiltro(row, segFiltro, filtro)) entries.push({ kind: 'row', row });
  }
  return entries;
}

/** Insere mantendo a ordem decrescente por data (futuro/mais recente primeiro) — mesma `insertSorted` do mockup. */
export function insertLancamentoOrdenado(rows: LancamentoRow[], novo: LancamentoRow): LancamentoRow[] {
  const idx = rows.findIndex((r) => r.data < novo.data);
  if (idx === -1) return [...rows, novo];
  return [...rows.slice(0, idx), novo, ...rows.slice(idx)];
}
