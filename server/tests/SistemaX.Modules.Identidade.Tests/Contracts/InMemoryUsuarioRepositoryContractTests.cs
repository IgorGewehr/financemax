using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

public sealed class InMemoryUsuarioRepositoryContractTests : UsuarioRepositoryContractTests
{
    protected override IUsuarioRepository CriarRepositorio() => new InMemoryUsuarioRepository();
}
