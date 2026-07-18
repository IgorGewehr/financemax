using SistemaX.Modules.Identidade.Application.Ports;

namespace SistemaX.Modules.Identidade.Tests.Fakes;

/// <summary>Relógio determinístico — mesmo molde de <c>Financeiro.Tests.Fakes.FakeRelogio</c>.</summary>
public sealed class FakeRelogio(DateTimeOffset agora) : IRelogio
{
    public DateTimeOffset Momento { get; set; } = agora;

    public DateTimeOffset Agora() => Momento;
}
