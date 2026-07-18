using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Infrastructure.Relogio;
using SistemaX.Modules.Identidade.Infrastructure.Sqlite;

namespace SistemaX.Modules.Identidade.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> de Identidade — registra os ADAPTERS concretos dos ports (mesmo
/// desenho de <c>FinanceiroInfrastructureModule</c>, mesmo racional de por que fica separado do
/// módulo de Application: grafo de referência <c>Infrastructure → Application → Domain</c>).
/// <see cref="ITentativaLoginStore"/> é SEMPRE in-memory, nos dois modos — ver comentário do port.
/// </summary>
public sealed class IdentidadeInfrastructureModule : IModule
{
    public string Codigo => "identidade.infra";
    public string Nome => "Identidade — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["identidade"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            services.AddScoped<IUsuarioRepository, SqliteUsuarioRepository>();
            services.AddScoped<IRefreshTokenRepository, SqliteRefreshTokenRepository>();
            services.AddModuleSchemaMigration<IdentidadeSchemaMigrationV1>();
            services.AddModuleSchemaMigration<IdentidadeSchemaMigrationV2>();
        }
        else
        {
            services.AddSingleton<IUsuarioRepository, InMemoryUsuarioRepository>();
            services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();
        }

        services.AddSingleton<ITentativaLoginStore, InMemoryTentativaLoginStore>();
        services.AddSingleton<IRelogio, RelogioSistema>();
    }
}
