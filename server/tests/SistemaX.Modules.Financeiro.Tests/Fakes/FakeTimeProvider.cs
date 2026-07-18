namespace SistemaX.Modules.Financeiro.Tests.Fakes;

/// <summary>TimeProvider determinístico — mesmo racional de <see cref="FakeRelogio"/>, pro
/// rollover mensal de <c>InMemoryConsultorOrcamentoLlm</c> ser testável sem esperar o calendário
/// virar de verdade.</summary>
public sealed class FakeTimeProvider(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Momento { get; set; } = agora;

    public override DateTimeOffset GetUtcNow() => Momento;
}
