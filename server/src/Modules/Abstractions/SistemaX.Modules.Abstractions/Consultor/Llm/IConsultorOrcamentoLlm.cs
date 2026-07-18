namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// Circuit-breaker de custo do narrador LLM — "teto mensal de custo por negócio; ao estourar, cai
/// pro template até o mês virar" (tarefa do Super Consultor). Sem <c>businessId</c> na assinatura
/// de propósito: <see cref="IConsultorNarrador.NarrarAsync"/> não carrega
/// <c>PeriodoRef</c>/<c>businessId</c> (contrato existente, não alterado por esta rodada) e o
/// financemax F1 é single-tenant fixo por instalação (um processo = um negócio, ver
/// <c>FinancemaxHost</c>/<c>ITenantsDeInstalacao</c>) — rastrear o orçamento por PROCESSO já
/// rastreia por negócio na prática. Orçamento por-tenant explícito é trabalho de uma fatia
/// multi-tenant futura, quando <c>IConsultorNarrador</c> ganhar <c>businessId</c>.
/// </summary>
public interface IConsultorOrcamentoLlm
{
    /// <summary>Falso quando o acumulado do mês corrente já atingiu o teto configurado —
    /// <see cref="NarradorLlm"/> nem tenta chamar a OpenAI nesse caso, cai direto pro template.</summary>
    bool PermiteChamada();

    /// <summary>Soma o custo estimado (centavos) da chamada que acabou de acontecer ao acumulado do
    /// mês corrente.</summary>
    void RegistrarCusto(long custoCentavosEstimado);
}
