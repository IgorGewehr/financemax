import type { Centavos } from '@/lib/money';

/**
 * View-model de "Entradas & saídas" (SDD) — espelha 1:1 os dados que
 * `docs/ui/mockups/entradas-saidas.html` manipula em JS. Todo dado numérico exibido é REAL
 * (`useEntradasSaidas.ts` → `GET /financeiro/extrato` + `relatorios/dre` + `fluxo`).
 */

/** Catálogo conhecido de categorias — usado só como override de rótulo em `categoriaLabel()`
 * (`calc.ts`) e no mapa do formulário de "Lançamento rápido". A Linha do tempo REAL
 * (`GET /financeiro/extrato`) pode devolver qualquer `categoriaId` cadastrado no domínio (ex.
 * `cmv-fornecedor`, `despesa-com-pessoal`) — por isso `LancamentoRow.categoria` é `string` livre,
 * nunca um `Record` indexado à força. */
export type CategoriaId = 'folha' | 'fornecedores' | 'aluguel' | 'impostos' | 'software' | 'marketing' | 'servicos';

export type TipoLancamento = 'entrada' | 'saida';
export type StatusLancamento = 'previsto' | 'pago' | 'atrasado';
export type SegFiltro = 'tudo' | 'receber' | 'pagar';

export type FiltroAtivo =
  // `value` é `string` livre — o filtro "Ver detalhe" do Super Consultor de Fornecedores compara
  // contra `LancamentoRow.categoria` REAL (`cmv-fornecedor`).
  | { type: 'categoria'; value: string; label: string }
  | { type: 'status'; value: 'atrasado'; label: string };

/** Uma linha da Linha do tempo (ExtratoUnificado: MovimentoFinanceiro + Parcela do domínio). */
export interface LancamentoRow {
  id: string;
  /** ISO yyyy-mm-dd. */
  data: string;
  desc: string;
  sub: string | null;
  /** `categoriaId` livre do domínio real — ver comentário de `CategoriaId` acima. */
  categoria: string;
  tipo: TipoLancamento;
  status: StatusLancamento;
  valorCentavos: Centavos;
  /** Preenchido quando `status === 'pago'`. */
  conta?: string;
  /** Preenchido quando `status === 'pago'`. */
  origem?: string;
  /** Preenchido quando `status === 'atrasado'`. */
  diasAtraso?: number;
}

export interface Atrasados30DiasResumo {
  totalCentavos: Centavos;
  qtdClientes: number;
}

export interface EntradasSaidasKpis {
  aReceberAbertoCentavos: Centavos;
  aReceberAtrasadoCentavos: Centavos;
  aReceberParcelasAbertas: number;
  aPagarAbertoCentavos: Centavos;
  aPagarMaiorLabel: string;
  aPagarMaiorData: string;
  aPagarLancamentosAbertos: number;
  resultadoMesCentavos: Centavos;
  resultadoDeltaPct: number;
  resultadoComparadoMes: string;
  fechamentoCaixaCentavos: Centavos;
}

/** Tradução caixa × competência (D.4 do contrato) — a nota logo abaixo dos KPIs. */
export interface BridgeNoteData {
  resultadoCentavos: Centavos;
  caixaCentavos: Centavos;
  diferimentoCentavos: Centavos;
}

export interface ConsultorFornecedoresData {
  deltaPct: number;
  mediaHistoricaCentavos: Centavos;
  totalMesCentavos: Centavos;
  qtdPagamentos: number;
}

export type TagConta = 'banco' | 'espécie';

export interface ContaDisponivel {
  nome: string;
  tag: TagConta;
}

/** Categorias oferecidas no formulário de "Lançamento rápido", por tipo. */
export interface CategoriasLancamentoRapido {
  entrada: string[];
  saida: string[];
}

/** Payload do formulário de "Lançamento rápido" ao salvar. */
export interface NovoLancamentoInput {
  tipo: TipoLancamento;
  descricao: string;
  categoriaLabel: string;
  /** Em reais (o input do form é decimal livre) — convertido p/ centavos ao persistir. */
  valorReais: number;
  vencimento: string;
  recorrente: boolean;
}

/** Entrada renderizável da Linha do tempo — linha real ou divisor "Hoje". */
export type TimelineEntry = { kind: 'row'; row: LancamentoRow } | { kind: 'divider'; label: string };

