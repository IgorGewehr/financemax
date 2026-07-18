using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application;
using SistemaX.Modules.Financeiro.Application.Endpoints;
using SistemaX.Modules.Financeiro.Infrastructure;
using SistemaX.Modules.Financeiro.Infrastructure.Cron;

namespace Financemax.Api.Composition;

/// <summary>
/// COMPOSITION ROOT do financemax (F1). Molde: <c>SistemaX.Host.Desktop.Composition.SistemaXHost</c>
/// — a mesma regra de ouro (o Core nunca conhece módulo concreto, só fala com
/// <see cref="ModuleRegistry"/>/<see cref="IModule"/>) reduzida ao único módulo desta fatia:
/// Financeiro (Application + Infrastructure + Endpoints). Vendas/Estoque/Compras/Fiscal/Identidade/
/// verticais do sistemax NÃO vêm — o financemax não tem PDV/estoque/clientes próprios, só o motor
/// financeiro (ver ARQUITETURA.md §1: "o DigiSat opera a loja; o financemax enxerga o dinheiro").
/// </summary>
public static class FinancemaxHost
{
    public static ModuleRegistry RegistrarModulos(
        IServiceCollection services,
        CamadaExecucao camada,
        IConfiguration configuracao,
        string businessId)
    {
        var contexto = new ModuleContext(camada, configuracao);

        var registry = new ModuleRegistry()
            .Adicionar(new FinanceiroModule())                // casos de uso + read-models + quant
            .Adicionar(new FinanceiroInfrastructureModule())   // adapters SQLite (persistencia=sqlite)
            .Adicionar(new FinanceiroEndpointsModule());       // /api/financeiro/*

        registry.RegistrarTodos(services, contexto);

        services.AddSingleton<IIntegrationEventBus, InProcessIntegrationEventBus>();

        // Super Consultor — orquestrador module-agnostic (mesmo racional do sistemax: não pertence
        // a nenhum IModule específico, vive no composition root). Narrador template determinístico
        // por enquanto — o IConsultorNarrador via LLM é F2 (junto com auth email+senha).
        services.AddSingleton<IConsultorInsightCache, InMemoryConsultorInsightCache>();
        services.AddScoped<IConsultorNarrador, NarradorTemplate>();
        services.AddScoped<ConsultorService>();

        // Infraestrutura local (SQLite + UoW/sessão + outbox + backup + migrations + crash-recovery)
        // — um único arquivo por instalação, dentro do volume /data do container (ver Dockerfile /
        // docker-compose.yml). Path é configurável via Financemax:DataDir (default: ./data).
        var dataDir = configuracao["Financemax:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        services.AddSistemaXLocalInfrastructure(o =>
        {
            o.DatabasePath = Path.Combine(dataDir, "financemax.db");
            o.BackupDirectory = Path.Combine(dataDir, "backups");
        });

        // F1 é single-tenant fixo (mesmo padrão do Host.Desktop: um processo = um businessId) — o
        // desenho multi-tenant (SQLite por tenant, §2.4 do ARQUITETURA.md) é trabalho de fatia
        // futura; os jobs de background (AvaliarParcelasVencidas/FaturarAssinaturas) já usam o
        // contrato ITenantsDeInstalacao, então a troca de implementação não toca em mais nada.
        services.AddSingleton<ITenantsDeInstalacao>(new TenantsDeInstalacaoFixo(businessId));

        services.AddOptions<FinanceiroCronOptions>();
        services.AddHostedService<AvaliarParcelasVencidasBackgroundService>();
        services.AddHostedService<FaturarAssinaturasBackgroundService>();

        // Exposto para o Program.cs enumerar OfType<IModuleEndpoints>() depois do builder.Build().
        services.AddSingleton(registry);

        return registry;
    }
}

/// <summary>Contexto de registro entregue a cada módulo — espelha
/// <c>SistemaX.Host.Desktop.Composition.ModuleContext</c>.</summary>
public sealed class ModuleContext(CamadaExecucao camada, IConfiguration configuracao) : IModuleContext
{
    public CamadaExecucao Camada => camada;
    public IConfiguration Configuracao => configuracao;
}
