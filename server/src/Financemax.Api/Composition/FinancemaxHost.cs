using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Abstractions.Consultor.Llm;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application;
using SistemaX.Modules.Financeiro.Application.Endpoints;
using SistemaX.Modules.Financeiro.Infrastructure;
using SistemaX.Modules.Financeiro.Infrastructure.Cron;
using SistemaX.Modules.Identidade.Application;
using SistemaX.Modules.Identidade.Application.Endpoints;
using SistemaX.Modules.Identidade.Infrastructure;

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
            .Adicionar(new FinanceiroEndpointsModule())        // /api/financeiro/*
            // F2 — auth e-mail+senha multi-usuário (MVP-ESCOPO.md, ARQUITETURA.md §3). Mesmo
            // desenho de 3 IModule que o Financeiro (Application/Infrastructure/Endpoints).
            .Adicionar(new IdentidadeModule())                 // casos de uso (login/refresh/CRUD usuário)
            .Adicionar(new IdentidadeInfrastructureModule())   // adapters SQLite (persistencia=sqlite)
            .Adicionar(new IdentidadeEndpointsModule());       // /api/auth/*, /api/usuarios/*

        registry.RegistrarTodos(services, contexto);

        services.AddSingleton<IIntegrationEventBus, InProcessIntegrationEventBus>();

        // Super Consultor — orquestrador module-agnostic (mesmo racional do sistemax: não pertence
        // a nenhum IModule específico, vive no composition root). NarradorLlm é o IConsultorNarrador
        // registrado — reescreve as frases determinísticas com gpt-4o-mini, com
        // NarradorTemplate SEMPRE por baixo como piso/fallback (sem chave, orçamento estourado,
        // rede falha, resposta inválida ou reprovada na validação anti-alucinação — ver
        // NarradorLlm). OPENAI_API_KEY ausente é degradação graciosa (checada dentro do
        // NarradorLlm, não aqui): o servidor sobe normal, só narra 100% via template.
        services.AddSingleton<IConsultorInsightCache, InMemoryConsultorInsightCache>();
        services.AddSingleton<NarradorTemplate>();

        var openAiOpcoes = OpenAiOptions.Resolver(configuracao);
        services.AddSingleton(openAiOpcoes);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConsultorOrcamentoLlm, InMemoryConsultorOrcamentoLlm>();
        services.AddSingleton<IConsultorNarracaoLlmCache, InMemoryConsultorNarracaoLlmCache>();
        services.AddHttpClient<IOpenAiChatClient, OpenAiHttpChatClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IConsultorNarrador, NarradorLlm>();
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
