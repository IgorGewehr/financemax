import { api, type Money } from './client';

export interface DisponivelParaRetiradaDto {
  saldoEmCaixa: Money;
  jaTemDono: Money;
  podeTirar: Money;
}

export interface PontoFluxoCaixaDto {
  data: string;
  entradas: Money;
  saidas: Money;
  saldoAcumulado: Money;
  projetado: boolean;
}

export interface FluxoDeCaixaDto {
  pontos: PontoFluxoCaixaDto[];
  primeiroDiaNegativo: string | null;
}

export interface ReceitaPorServicoDto {
  servicoId: string;
  servicoNome: string;
  mrr: Money;
  percentual: number;
}

export interface ReceitaRecorrenteDto {
  mrr: Money;
  arr: Money;
  assinaturasAtivas: number;
  ticketMedio: Money;
  mrrNovoNoMes: Money;
  mrrChurnNoMes: Money;
  clientesChurnNoMes: number;
  churnPercent: number;
  porServico: ReceitaPorServicoDto[];
  maiorConcentracao: ReceitaPorServicoDto | null;
}

// ── F0/F1 — motor quant + fact tables (docs/financeiro/inteligencia-arquitetura.md/ADR-0005) ──
// Diferença importante destes DTOs pros de cima: os valores em centavos aqui são `number` PURO
// (long serializado, sem `moeda`), não `Money` — os read-models de `Application.ReadModels` que
// os produzem (`PrevisaoDeCaixaResultado`, `PontoDeEquilibrioResultado`, `InadimplenciaResultado`,
// `RadarDoSimplesResultado`) devolvem `long` cru, ver os `record` em
// `SistemaX.Modules.Financeiro.Application/ReadModels/*.cs`. Enums (`FaixaDeAtraso`) também vêm
// como NÚMERO (ordinal) — o projeto não registra `JsonStringEnumConverter` em nenhum `Program.cs`
// nem os endpoints fazem `.ToString()` neles (diferente de `VendaDto.status`, que converte
// explicitamente) — ver `FinanceiroEndpointsModule.MapearEndpoints`.

export interface BandaDeCaixaDto {
  data: string;
  p5Centavos: number;
  p50Centavos: number;
  p95Centavos: number;
}

export interface PrevisaoDeCaixaDto {
  bandas: BandaDeCaixaDto[];
  probabilidadeSaldoNegativoEm30Dias: number;
  primeiroDiaP50Negativo: string | null;
  diasRunwayBruto: number | null;
  diasRunwayRealista: number | null;
}

export interface PontoDeEquilibrioDto {
  custosFixosMensaisCentavos: number;
  margemContribuicaoPercentual: number;
  receitaNecessariaMensalCentavos: number;
  receitaNecessariaDiariaCentavos: number;
  receitaAcumuladaNoMesCentavos: number;
  diaDoEquilibrio: number | null;
  jaAtingiuNoMes: boolean;
}

/** Ordinal de `FaixaDeAtraso` (.NET): 0 EmDia, 1 Ate30Dias, 2 De31a60Dias, 3 De61a90Dias,
 * 4 De91a180Dias, 5 Acima180Dias — ver `Quant/InadimplenciaRollRate.cs`. */
export type FaixaDeAtrasoOrdinal = 0 | 1 | 2 | 3 | 4 | 5;

export interface ResumoFaixaDeAtrasoDto {
  faixa: FaixaDeAtrasoOrdinal;
  valorCentavos: number;
  provisaoCentavos: number;
  quantidade: number;
}

export interface InadimplenciaDto {
  valorTotalEmAbertoCentavos: number;
  provisaoEsperadaCentavos: number;
  valorLiquidoEsperadoCentavos: number;
  porFaixa: ResumoFaixaDeAtrasoDto[];
}

export interface RadarDoSimplesDto {
  rbt12Centavos: number;
  faixaAtual: number;
  aliquotaEfetiva: number;
  aliquotaNominalFaixaAtual: number;
  distanciaAoProximoDegrauCentavos: number;
  mesesProjetadosAteOProximoDegrau: number | null;
}

export interface FatoReceitaDiariaDto {
  tenantId: string;
  dia: string;
  receitaCentavos: number;
  atualizadoEmUtc: string;
}

export interface FatoCaixaDiarioDto {
  tenantId: string;
  dia: string;
  entradasCentavos: number;
  saidasCentavos: number;
  atualizadoEmUtc: string;
  /** Propriedade computada (`get`) do record .NET — vem no JSON como qualquer outra, mas nunca é
   * gravada, só derivada de `entradasCentavos - saidasCentavos` no servidor. */
  saldoDiaCentavos: number;
}

export interface FatoCustoDiarioDto {
  tenantId: string;
  dia: string;
  custoCentavos: number;
  atualizadoEmUtc: string;
}

export interface FatoMargemProdutoDto {
  tenantId: string;
  produtoId: string;
  dia: string;
  receitaCentavos: number;
  custoCentavos: number;
  atualizadoEmUtc: string;
  margemContribuicaoCentavos: number;
}

// ── FASE 2 — Super Consultor (docs/financeiro/inteligencia-arquitetura.md §3.5/ADR-0005) ──
// `GET /financeiro/consultor` devolve `IReadOnlyList<ConsultorInsightNarrado>` (.NET,
// `Abstractions.Consultor`): insights JÁ narrados e rankeados, cada um com a `frase` pronta +
// os `facts` crus pré-formatados (para o painel "Ver como calculamos", que nunca depende do LLM)
// + o `drill` de navegação read-only (Lei 2 — a IA aponta, nunca age).
//
// `origem` é o ordinal de `ConsultorNarracaoOrigem` (0 Template, 1 Llm) — vem como NÚMERO pelo
// mesmo motivo dos enums da F1 acima (nenhum `JsonStringEnumConverter` registrado). Hoje é sempre
// 0: o narrador registrado é o `NarradorTemplate` (determinístico, custo zero). `facts`/`drill.
// parametros` são dicionários string→string; `drill` pode ser null.

export interface ConsultorDrillDto {
  tela: string;
  parametros?: Record<string, string> | null;
}

export interface ConsultorInsightDto {
  modulo: string;
  ruleId: string;
  tela: string;
  score: number;
  frase: string;
  /** Ordinal de `ConsultorNarracaoOrigem` (.NET): 0 Template, 1 Llm — enum como número. */
  origem: number;
  facts: Record<string, string>;
  drill: ConsultorDrillDto | null;
}

function periodoQuery(de?: string, ate?: string): string {
  const params = new URLSearchParams();
  if (de) params.set('de', de);
  if (ate) params.set('ate', ate);
  const qs = params.toString();
  return qs ? `?${qs}` : '';
}

// ── Bancário (docs/wiring/financeiro-telas-restantes.md §3) — contas/formas + extrato +
// conciliação + taxas por forma. `Money` nos volumes/saldos/taxas; `taxaPercentual`/`mdrPercentual`
// vêm como fração crua (0.0349 = 3,49%), o mesmo formato de `FormaDePagamento.TaxaPercentual` (.NET).

export interface ContaBancariaDto {
  id: string;
  nome: string;
  tipo: string;
  saldo: Money;
  ativa: boolean;
}

export interface FormaDePagamentoDto {
  id: string;
  nome: string;
  tipo: string;
  mdrPercentual: number;
  lagLiquidacaoDias: number;
  contaLiquidacaoId: string | null;
  ativo: boolean;
}

export interface MovimentoBancarioDto {
  id: string;
  data: string;
  descricao: string;
  forma: string;
  contaBancariaCaixaId: string;
  /** Já vem COM SINAL (positivo entrada, negativo saída) — nunca recalcular o sinal no front. */
  valor: Money;
  conciliado: boolean;
}

export interface DiaMovimentoDto {
  dia: string;
  entradas: Money;
  saidas: Money;
}

export interface SemanaMovimentoDto {
  numero: number;
  inicio: string;
  fim: string;
  parcial: boolean;
  dias: DiaMovimentoDto[];
}

export interface ItemBatidoAmostraDto {
  data: string;
  descricao: string;
}

export interface ItemConciliacaoPendenteDto {
  id: string;
  data: string;
  descricao: string;
  valor: Money;
  sugestao: string | null;
  /** Id do melhor candidato do lado oposto (extrato↔movimento) — pronto pro par de
   * `confirmarConciliacao`/`ignorarConciliacao`. `null` quando a heurística não achou candidato. */
  idSugerido: string | null;
}

export interface ConciliacaoBancariaDto {
  bateuCertinhoTotal: number;
  bateuCertinhoAmostra: ItemBatidoAmostraDto[];
  sobrouNoBanco: ItemConciliacaoPendenteDto[];
  sobrouNoSistema: ItemConciliacaoPendenteDto[];
}

export interface ConciliacaoDto {
  id: string;
  movimentoFinanceiroId: string;
  extratoBancarioItemId: string;
  status: string;
  conciliadoEm: string | null;
}

export interface TaxaPorFormaDto {
  formaPagamentoId: string;
  forma: string;
  volume: Money;
  taxaPercentual: number;
  taxa: Money;
}

export interface TaxasPorFormaDto {
  taxaTotal: Money;
  volumeTotal: Money;
  percentualVolume: number;
  porForma: TaxaPorFormaDto[];
}

// ── Simulador de empréstimo (Bancário) — cálculo puro server-side (Tabela Price), sem persistência
// nem side-effect: o usuário simula, não a IA age (Lei 2 não se aplica aqui). Centavos em `number`
// PURO (mesma convenção da F0/F1), não `Money` — não há moeda estrangeira envolvida no cálculo.

export interface SimularEmprestimoRequest {
  valorCentavos: number;
  /** Taxa de juros ao mês, em basis points (1% a.m. = 100 bps) — evita float em campo de negócio. */
  taxaJurosMensalBps: number;
  prazoMeses: number;
  /** Retorno mensal esperado do equipamento financiado, opcional — habilita o cálculo de payback. */
  retornoMensalEsperadoCentavos?: number | null;
}

export type VereditoEmprestimo = 'viavel' | 'apertado' | 'inviavel';

export interface SimulacaoEmprestimoDto {
  parcelaMensalCentavos: number;
  custoTotalCentavos: number;
  jurosTotaisCentavos: number;
  taxaEfetivaAnualPercent: number;
  /** Só preenchido quando `retornoMensalEsperadoCentavos` foi informado no request. */
  paybackMeses: number | null;
  veredito: VereditoEmprestimo;
  motivo: string;
}

// ── Entradas & Saídas / Relatórios / Recorrentes — reconciliação de
// docs/wiring/financeiro-telas-restantes.md (task #33): extrato unificado, DRE gerencial
// (competência), contas em aberto (aging) e o detalhe nominal de assinaturas/contas fixas.

export interface ExtratoLinhaDto {
  id: string;
  data: string;
  descricao: string;
  categoriaId: string;
  tipo: 'entrada' | 'saida';
  status: 'previsto' | 'pago' | 'atrasado';
  valor: Money;
  conta?: string | null;
  origem?: string | null;
  diasAtraso?: number | null;
}

export interface ExtratoKpisDto {
  totalEntradas: Money;
  totalSaidas: Money;
  saldoPeriodo: Money;
}

export interface ExtratoDto {
  linhas: ExtratoLinhaDto[];
  kpis: ExtratoKpisDto;
}

/** Ordinal de `CorrenteDeReceita` (.NET, `Comum/CorrenteDeReceita.cs`) — VALORES PINADOS, nunca
 * reordenar: 0 Recorrente (MRR de assinatura), 1 Servico (OS — mão de obra + peças), 2 Comercio
 * (venda avulsa de balcão/delivery). Sem `JsonStringEnumConverter` registrado pra este enum (igual
 * `FaixaDeAtraso`) — chega como número cru. */
export type CorrenteDeReceitaOrdinal = 0 | 1 | 2;

/** `DrePorCorrente` (.NET) — unit economics de UMA corrente dentro do DRE do período (P0-1). Σ
 * `porCorrente[].receitaBruta` ≤ `DreDto.receitaBruta` (igual quando toda receita do período está
 * tagueada com uma corrente conhecida). Usado pelo mix "De onde vem" da Visão Geral v3. */
export interface DrePorCorrenteDto {
  corrente: CorrenteDeReceitaOrdinal;
  receitaBruta: Money;
  custoDireto: Money;
  margem: Money;
}

/** DRE gerencial simplificado, POR COMPETÊNCIA — `DreGerencialService` (regime de caixa ainda não
 * implementado no backend, ver docs/wiring/financeiro-telas-restantes.md §5). */
export interface DreDto {
  receitaBruta: Money;
  custoDireto: Money;
  despesaOperacional: Money;
  resultadoOperacional: Money;
  porCorrente: DrePorCorrenteDto[];
}

export interface AgingBucketDto {
  id: string;
  label: string;
  valor: Money;
}

export interface ContasEmAbertoDto {
  receberEmAberto: Money;
  receberAtrasado: Money;
  pagarEmAberto: Money;
  agingBuckets: AgingBucketDto[];
}

/** Linha nominal de "Todas as assinaturas" — `AssinaturaDetalheService`, só assinaturas ATIVAS. */
export interface RecorrenteDetalheDto {
  id: string;
  clienteId: string;
  clienteNome: string;
  servicoId: string;
  servicoNome: string;
  valorPorCiclo: Money;
  ciclo: string;
  status: string;
  proximaCobranca: string;
}

/** Template de recorrência ativo — `ContasFixasService` (SÓ o template; histórico/variação/emAlerta
 * fora de escopo, ver comentário no serviço .NET). */
export interface ContaFixaResumoDto {
  id: string;
  descricao: string;
  categoriaId: string;
  valorPrevisto: Money;
  diaFixo: number | null;
  frequencia: string;
  tipo: string;
  proximaOcorrencia?: string | null;
}

// ── Fluxo de Caixa — ritual do caixa físico em espécie (SessaoCaixa). NÃO confundir com
// `financeiroApi.fluxo` (projeção de saldo da Visão Geral) — colisão de nome só, ver
// docs/wiring/financeiro-telas-restantes.md §4.

export interface MovimentoSessaoCaixaDto {
  id: string;
  tipo: 'suprimento' | 'sangria' | 'vendaEmEspecie';
  valorCentavos: number;
  motivo: string | null;
  registradoEm: string;
  operadorId: string;
  operadorNome: string;
}

export interface SessaoCaixaDto {
  id: string;
  contaCaixaId: string;
  operadorId: string;
  operadorNome: string;
  status: string;
  abertaEm: string;
  saldoAberturaCentavos: number;
  totalEntradasCentavos: number;
  totalSaidasCentavos: number;
  saldoEsperadoCentavos: number;
  fechadaEm: string | null;
  saldoInformadoCentavos: number | null;
  diferencaCentavos: number | null;
  movimentos: MovimentoSessaoCaixaDto[];
}

export interface AbrirCaixaRequest {
  saldoAberturaCentavos: number;
  operadorId: string;
  operadorNome: string;
  contaCaixaId?: string;
}

export interface SuprimentoRequest {
  sessaoId: string;
  valorCentavos: number;
  motivo: string;
  operadorId: string;
  operadorNome: string;
}

export interface SangriaRequest {
  sessaoId: string;
  valorCentavos: number;
  motivo: string;
  operadorId: string;
  operadorNome: string;
}

export interface FecharCaixaRequest {
  sessaoId: string;
  contadoCentavos: number;
}

// ── Análise por Projeto + Imobilizado/ROI (docs/financeiro/design-analise-por-projeto.md §9,
// docs/financeiro/design-imobilizado-roi.md §7) — DTOs de fio 1:1 com `FinanceiroEndpointsModule`/
// `PainelDoProjetoService`/`RoiDoNegocioService` (.NET). `DateOnly` do .NET serializa como string
// ISO `yyyy-MM-dd`; `Money` como `{ centavos, moeda }` — mesma convenção do resto deste arquivo.

/** `ConfiguracaoFinanceiraDto` (.NET) — os dois toggles opt-in do Financeiro + os 3 campos do
 * segundo toggle (Imobilizado/ROI). Mesmo shape serve de request no `PUT` (o endpoint recria a
 * config inteira a cada gravação — nunca um PATCH parcial). */
export interface ConfiguracaoFinanceiraDto {
  analisePorProjetoAtiva: boolean;
  custoHoraPadraoCentavos: number | null;
  tempoEntraNoDre: boolean;
  imobilizadoRoiAtivo: boolean;
  taxaDescontoAnualBps: number | null;
  inicioOperacao: string | null;
}

export interface ProjetoDto {
  id: string;
  nome: string;
  descricao: string | null;
  status: string;
  criadoEm: string;
  arquivadoEm: string | null;
}

export interface CriarProjetoRequest {
  nome: string;
  descricao?: string | null;
}

export interface PainelReceitaProjetoDto {
  mrr: Money;
  arr: Money;
  assinaturasAtivas: number;
  ticketMedio: Money;
}

export interface PainelChurnProjetoDto {
  cancelamentos12m: number;
  exposicaoAssinaturaMeses12m: number;
  churnMensalPercent: number;
  vidaEsperadaMeses: number | null;
}

export interface PainelLtvProjetoDto {
  ltv: Money | null;
  limiteInferior: Money;
  metodo: string;
  observacao: string | null;
}

export interface PainelMargemProjetoDto {
  competencia: string;
  receita: Money;
  custoDireto: Money;
  mc1: Money;
  mc1Percent: number;
  amortizacaoMes: Money;
  mc2: Money;
  mc2Percent: number;
  custoTempoMes: Money | null;
  mc3: Money | null;
  mc3Percent: number | null;
}

export interface PainelCapacidadeProjetoDto {
  unidadesTotais: number;
  unidadesUtilizadas: number;
  utilizacaoPercent: number;
  custoOciosidadeMesCentavos: number;
}

export interface PainelPaybackProjetoDto {
  investimentoTotalCentavos: number;
  fluxoCaixaAcumuladoCentavos: number;
  paybackRealizadoEm: string | null;
  paybackProjetadoMeses: number | null;
  metodo: string;
}

export interface PainelRoiProjetoDto {
  realizadoPercent: number | null;
  roiSobreInvestimentoPercent: number | null;
  runRateAnualizadoPercent: number | null;
}

export interface PainelTempoPorClienteDto {
  clienteId: string;
  clienteNome: string | null;
  minutos: number;
  custoCentavos: number | null;
}

export interface PainelTempoProjetoDto {
  minutosJanela: number;
  custoJanelaCentavos: number | null;
  porCliente: PainelTempoPorClienteDto[];
}

/** `PainelDoProjetoResultado` (.NET) — `GET /financeiro/projetos/{id}/painel`. */
export interface PainelDoProjetoDto {
  projeto: ProjetoDto;
  receita: PainelReceitaProjetoDto;
  churn: PainelChurnProjetoDto;
  ltv: PainelLtvProjetoDto;
  margem: PainelMargemProjetoDto;
  capacidade: PainelCapacidadeProjetoDto;
  payback: PainelPaybackProjetoDto;
  roi: PainelRoiProjetoDto;
  tempo: PainelTempoProjetoDto;
}

/** `AtivoDeCapitalDto` (.NET) — reusado por Análise por Projeto (licenças/intangível) E por
 * Imobilizado (bens tangíveis) — "um agregado só, dois gates" (design-imobilizado-roi.md §8.1). */
export interface AtivoDeCapitalDto {
  id: string;
  projetoId: string | null;
  nome: string;
  natureza: string;
  categoria: string;
  custoAquisicaoCentavos: number;
  valorResidualCentavos: number;
  dataAquisicao: string;
  inicioDepreciacao: string;
  vidaUtilMeses: number;
  quantidadeUnidades: number;
  contaAPagarId: string | null;
  status: string;
  ultimaCompetenciaReconhecida: string | null;
  encerradoEm: string | null;
  baixadoEm: string | null;
  motivoBaixa: string | null;
  valorContabilAtualCentavos: number;
  amortizacaoMensalCentavos: number;
  valorVendaCentavos: number | null;
  resultadoAlienacaoCentavos: number | null;
}

export interface ParcelaInvestimentoRequest {
  vencimento: string;
  valorCentavos: number;
}

export interface CriarAtivoDeCapitalRequest {
  nome: string;
  natureza: string;
  categoria: string;
  custoAquisicaoCentavos: number;
  dataAquisicao: string;
  vidaUtilMeses: number;
  valorResidualCentavos?: number;
  inicioDepreciacao?: string | null;
  quantidadeUnidades?: number;
  projetoId?: string | null;
  parcelas?: ParcelaInvestimentoRequest[] | null;
  contaAPagarId?: string | null;
}

export interface AporteDeCapitalDto {
  id: string;
  valorCentavos: number;
  data: string;
  descricao: string;
  criadoEm: string;
}

export interface RegistrarAporteDeCapitalRequest {
  valorCentavos: number;
  data: string;
  descricao: string;
}

export interface RoiSerieMensalDto {
  competencia: string;
  fluxoOperacionalCentavos: number;
  capexCentavos: number;
  aporteCentavos: number;
  liquidoCentavos: number;
  acumuladoCentavos: number;
  acumuladoDescontadoCentavos: number;
}

export interface RoiPorCategoriaDto {
  categoria: string;
  custoCentavos: number;
  valorContabilCentavos: number;
  vendidos: number;
  resultadoAlienacaoCentavos: number;
}

export interface RoiInvestimentoDto {
  capexCentavos: number;
  aportesCentavos: number;
  totalCentavos: number;
  giroConsumidoObservadoCentavos: number;
  bens: number;
  porCategoria: RoiPorCategoriaDto[];
  resultadoAlienacaoTotalCentavos: number;
}

export interface RoiRecuperacaoDto {
  fluxoOperacionalAcumuladoCentavos: number;
  recuperadoCentavos: number;
  faltamCentavos: number;
  percentRecuperado: number;
}

export interface RoiPaybackDto {
  simplesRealizadoEm: string | null;
  descontadoRealizadoEm: string | null;
  projetadoMeses: number | null;
  descontadoProjetadoMeses: number | null;
  metodo: string;
}

export interface RoiTirDto {
  mensalPercent: number | null;
  anualizadaPercent: number | null;
  motivoIndefinida: string | null;
}

export interface RoiPercentuaisDto {
  caixaPercent: number;
  competenciaPercent: number;
  mesesAteRoiCompleto: number | null;
}

/** `RoiDoNegocioResultado` (.NET) — `GET /financeiro/roi-negocio`. 404 com o toggle desligado. */
export interface RoiDoNegocioDto {
  marcoInicial: string;
  taxaDescontoAnualBps: number | null;
  investimento: RoiInvestimentoDto;
  recuperacao: RoiRecuperacaoDto;
  payback: RoiPaybackDto;
  tir: RoiTirDto;
  roi: RoiPercentuaisDto;
  serie: RoiSerieMensalDto[];
}

export const financeiroApi = {
  disponivelParaRetirada: () => api.get<DisponivelParaRetiradaDto>('/financeiro/disponivel-retirada'),
  fluxo: (diasHistorico = 14, diasProjecao = 30) =>
    api.get<FluxoDeCaixaDto>(`/financeiro/fluxo?diasHistorico=${diasHistorico}&diasProjecao=${diasProjecao}`),
  receitaRecorrente: () => api.get<ReceitaRecorrenteDto>('/financeiro/receita-recorrente'),

  // Motor quant da F1 — as 4 leituras que alimentam o bloco de Sobrevivência.
  previsaoCaixa: (dias = 30) => api.get<PrevisaoDeCaixaDto>(`/financeiro/previsao-caixa?dias=${dias}`),
  pontoEquilibrio: () => api.get<PontoDeEquilibrioDto>('/financeiro/ponto-equilibrio'),
  inadimplencia: () => api.get<InadimplenciaDto>('/financeiro/inadimplencia'),
  radarSimples: (anexo = 'I') => api.get<RadarDoSimplesDto>(`/financeiro/radar-simples?anexo=${anexo}`),

  // Super Consultor da Visão Geral — insights já narrados/rankeados (Fase 2).
  consultor: (topN?: number) =>
    api.get<ConsultorInsightDto[]>(`/financeiro/consultor${topN ? `?topN=${topN}` : ''}`),

  // Fact tables da F0 — consulta direta, sem read-model por cima ainda (série bruta).
  fatoReceitaDiaria: (de?: string, ate?: string) =>
    api.get<FatoReceitaDiariaDto[]>(`/financeiro/fato-receita-diaria${periodoQuery(de, ate)}`),
  fatoCaixaDiario: (de?: string, ate?: string) =>
    api.get<FatoCaixaDiarioDto[]>(`/financeiro/fato-caixa-diario${periodoQuery(de, ate)}`),
  fatoCustoDiario: (de?: string, ate?: string) =>
    api.get<FatoCustoDiarioDto[]>(`/financeiro/fato-custo-diario${periodoQuery(de, ate)}`),
  fatoMargemProduto: (produtoId?: string, de?: string, ate?: string) => {
    const params = new URLSearchParams();
    if (produtoId) params.set('produtoId', produtoId);
    if (de) params.set('de', de);
    if (ate) params.set('ate', ate);
    const qs = params.toString();
    return api.get<FatoMargemProdutoDto[]>(`/financeiro/fato-margem-produto${qs ? `?${qs}` : ''}`);
  },

  // Bancário — contas com saldo real, formas com MDR/lag, extrato, agregação semanal, os 3
  // baldes de conciliação (+ confirmar/ignorar) e o painel "Ver por forma" do Super Consultor.
  contasBancarias: () => api.get<ContaBancariaDto[]>('/financeiro/contas-bancarias'),
  formasPagamento: () => api.get<FormaDePagamentoDto[]>('/financeiro/formas-pagamento'),
  movimentos: (de?: string, ate?: string, contaId?: string) => {
    const params = new URLSearchParams();
    if (de) params.set('de', de);
    if (ate) params.set('ate', ate);
    if (contaId) params.set('contaId', contaId);
    const qs = params.toString();
    return api.get<MovimentoBancarioDto[]>(`/financeiro/movimentos${qs ? `?${qs}` : ''}`);
  },
  movimentosSemana: (de?: string, ate?: string) =>
    api.get<SemanaMovimentoDto[]>(`/financeiro/movimentos-semana${periodoQuery(de, ate)}`),
  conciliacao: (de?: string, ate?: string) =>
    api.get<ConciliacaoBancariaDto>(`/financeiro/conciliacao${periodoQuery(de, ate)}`),
  confirmarConciliacao: (movimentoFinanceiroId: string, extratoBancarioItemId: string, automatico = false) =>
    api.post<ConciliacaoDto>('/financeiro/conciliacao', { movimentoFinanceiroId, extratoBancarioItemId, automatico }),
  ignorarConciliacao: (movimentoFinanceiroId: string, extratoBancarioItemId: string) =>
    api.post<ConciliacaoDto>('/financeiro/conciliacao/ignorar', { movimentoFinanceiroId, extratoBancarioItemId }),
  taxasPorForma: (de?: string, ate?: string) =>
    api.get<TaxasPorFormaDto>(`/financeiro/taxas-por-forma${periodoQuery(de, ate)}`),
  simularEmprestimo: (payload: SimularEmprestimoRequest) =>
    api.post<SimulacaoEmprestimoDto>('/financeiro/bancario/simular-emprestimo', payload),

  // Entradas & Saídas / Relatórios — extrato unificado, DRE (competência) e contas em aberto.
  extrato: (de?: string, ate?: string, tipo?: 'entrada' | 'saida', categoria?: string) => {
    const params = new URLSearchParams();
    if (de) params.set('de', de);
    if (ate) params.set('ate', ate);
    if (tipo) params.set('tipo', tipo);
    if (categoria) params.set('categoria', categoria);
    const qs = params.toString();
    return api.get<ExtratoDto>(`/financeiro/extrato${qs ? `?${qs}` : ''}`);
  },
  relatoriosDre: (de?: string, ate?: string) => api.get<DreDto>(`/financeiro/relatorios/dre${periodoQuery(de, ate)}`),
  relatoriosContasEmAberto: () => api.get<ContasEmAbertoDto>('/financeiro/relatorios/contas-em-aberto'),

  // Recorrentes — detalhe nominal por assinatura e template de contas fixas.
  recorrentesDetalhe: () => api.get<RecorrenteDetalheDto[]>('/financeiro/recorrentes/detalhe'),
  recorrentesFixas: () => api.get<ContaFixaResumoDto[]>('/financeiro/recorrentes/fixas'),

  // Fluxo de Caixa — ritual do caixa físico (SessaoCaixa).
  caixaAtual: (contaCaixaId?: string) =>
    api.get<SessaoCaixaDto | null>(`/financeiro/caixa/atual${contaCaixaId ? `?contaCaixaId=${contaCaixaId}` : ''}`),
  caixaHistorico: (contaCaixaId?: string, de?: string, ate?: string) => {
    const params = new URLSearchParams();
    if (contaCaixaId) params.set('contaCaixaId', contaCaixaId);
    if (de) params.set('de', de);
    if (ate) params.set('ate', ate);
    const qs = params.toString();
    return api.get<SessaoCaixaDto[]>(`/financeiro/caixa/historico${qs ? `?${qs}` : ''}`);
  },
  abrirCaixa: (payload: AbrirCaixaRequest) => api.post<SessaoCaixaDto>('/financeiro/caixa/abrir', payload),
  caixaSuprimento: (payload: SuprimentoRequest) => api.post<SessaoCaixaDto>('/financeiro/caixa/suprimento', payload),
  caixaSangria: (payload: SangriaRequest) => api.post<SessaoCaixaDto>('/financeiro/caixa/sangria', payload),
  caixaFechar: (payload: FecharCaixaRequest) => api.post<SessaoCaixaDto>('/financeiro/caixa/fechar', payload),

  // Análise por Projeto + Imobilizado/ROI (docs/financeiro/design-analise-por-projeto.md,
  // docs/financeiro/design-imobilizado-roi.md) — dois toggles opt-in independentes em
  // `ConfiguracaoFinanceiraTenant`. Desligado: `projetos`/`imobilizado`/`aportes` devolvem `[]`
  // (nunca erro); `roiNegocio` devolve 404 (é um painel, não uma listagem) — ver
  // `FinanceiroEndpointsModule.MapearEndpoints` linhas 744-1027.
  configuracoes: () => api.get<ConfiguracaoFinanceiraDto>('/financeiro/configuracoes'),
  salvarConfiguracoes: (payload: ConfiguracaoFinanceiraDto) =>
    api.put<ConfiguracaoFinanceiraDto>('/financeiro/configuracoes', payload),

  projetos: (incluirArquivados = false) =>
    api.get<ProjetoDto[]>(`/financeiro/projetos${incluirArquivados ? '?incluirArquivados=true' : ''}`),
  criarProjeto: (payload: CriarProjetoRequest) => api.post<ProjetoDto>('/financeiro/projetos', payload),
  projetoPainel: (id: string) => api.get<PainelDoProjetoDto>(`/financeiro/projetos/${id}/painel`),

  imobilizado: () => api.get<AtivoDeCapitalDto[]>('/financeiro/imobilizado'),
  criarImobilizado: (payload: CriarAtivoDeCapitalRequest) => api.post<AtivoDeCapitalDto>('/financeiro/imobilizado', payload),

  aportes: () => api.get<AporteDeCapitalDto[]>('/financeiro/aportes'),
  criarAporte: (payload: RegistrarAporteDeCapitalRequest) => api.post<AporteDeCapitalDto>('/financeiro/aportes', payload),
  excluirAporte: (id: string) => api.delete<void>(`/financeiro/aportes/${id}`),

  roiNegocio: () => api.get<RoiDoNegocioDto>('/financeiro/roi-negocio'),
};
