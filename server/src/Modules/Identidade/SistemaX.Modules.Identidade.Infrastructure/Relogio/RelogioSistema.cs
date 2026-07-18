using SistemaX.Modules.Identidade.Application.Ports;

namespace SistemaX.Modules.Identidade.Infrastructure.Relogio;

public sealed class RelogioSistema : IRelogio
{
    public DateTimeOffset Agora() => DateTimeOffset.UtcNow;
}
