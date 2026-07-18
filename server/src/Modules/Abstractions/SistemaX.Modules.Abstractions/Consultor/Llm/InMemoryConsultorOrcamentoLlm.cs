namespace SistemaX.Modules.Abstractions.Consultor.Llm;

/// <summary>
/// Implementação in-memory de <see cref="IConsultorOrcamentoLlm"/> — suficiente para F1
/// (single-tenant, um processo por instalação; um restart mensal acidental no pior caso reseta o
/// contador cedo demais, o que é sempre o lado SEGURO do erro: gasta um pouco mais, nunca nega
/// serviço além do configurado). Registrada como singleton (o contador precisa sobreviver entre
/// requisições). <see cref="TimeProvider"/> injetado (não <c>DateTimeOffset.UtcNow</c> direto) para
/// o rollover de mês ser testável sem esperar o calendário virar de verdade.
/// </summary>
public sealed class InMemoryConsultorOrcamentoLlm(TimeProvider relogio, OpenAiOptions opcoes) : IConsultorOrcamentoLlm
{
    private readonly Lock _portao = new();
    private string _mesAtual = string.Empty;
    private long _acumuladoCentavos;

    public bool PermiteChamada()
    {
        lock (_portao)
        {
            RolarMesSeNecessario();
            return _acumuladoCentavos < opcoes.OrcamentoMensalCentavos;
        }
    }

    public void RegistrarCusto(long custoCentavosEstimado)
    {
        if (custoCentavosEstimado <= 0) return;

        lock (_portao)
        {
            RolarMesSeNecessario();
            _acumuladoCentavos += custoCentavosEstimado;
        }
    }

    /// <summary>Chamado sempre dentro do lock — "mês" é a chave <c>yyyy-MM</c> em UTC, mesma
    /// granularidade que o resto do sistema usa pra "hoje" (<c>PeriodoRef.Dia</c>).</summary>
    private void RolarMesSeNecessario()
    {
        var mesAgora = relogio.GetUtcNow().ToString("yyyy-MM");
        if (mesAgora == _mesAtual) return;

        _mesAtual = mesAgora;
        _acumuladoCentavos = 0;
    }
}
