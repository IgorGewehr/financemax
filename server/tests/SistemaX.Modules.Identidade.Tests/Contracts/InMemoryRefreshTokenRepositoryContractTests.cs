using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

public sealed class InMemoryRefreshTokenRepositoryContractTests : RefreshTokenRepositoryContractTests
{
    protected override IRefreshTokenRepository CriarRepositorio() => new InMemoryRefreshTokenRepository();
}
