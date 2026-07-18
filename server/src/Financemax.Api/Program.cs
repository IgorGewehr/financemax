using System.Net;
using Financemax.Api.Auth;
using Financemax.Api.Bridge;
using Financemax.Api.Composition;
using Serilog;
using Serilog.Events;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Infrastructure.Seed;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// financemax F1 — servidor Financeiro headless. Molde: SistemaX.Host.Desktop/Program.cs, reduzido
// ao que um container Linux precisa: sem Velopack (updater), sem Photino (janela), sem boot-token/
// PIN (StubAuthMiddleware até a F2 trazer e-mail+senha real). Kestrel escuta 0.0.0.0:8080 dentro
// do container. Hoje (F1) o docker-compose.yml PUBLICA essa porta pra fora ("ports:") porque
// cloudflared/Litestream ainda não existem no compose — é a topologia provisória de F1 (uso
// local/VM sem túnel). O plano de produção (ARQUITETURA.md §4) é cloudflared-only, sem "ports:"
// publicada: isso entra na F2, junto com a auth de verdade; quando endurecer, remover "ports:" e
// atualizar este comentário.
// ─────────────────────────────────────────────────────────────────────────────────────────────

var iniciadoEm = DateTimeOffset.UtcNow;

var builder = WebApplication.CreateBuilder(args);

// businessId fixo desta instalação (F1 é single-tenant — ver StubAuthMiddleware/FinancemaxHost).
// Financemax:BusinessId (appsettings/env FINANCEMAX__BUSINESSID) com fallback de dev.
var businessId = builder.Configuration["Financemax:BusinessId"] ?? "dev-tenant";

// persistencia=sqlite alimenta o mesmo IConfiguration que FinanceiroInfrastructureModule lê para
// decidir SQLite x in-memory (ver FinanceiroInfrastructureModule.Registrar) — sempre sqlite aqui,
// nunca in-memory: um servidor headless sem persistência não serve a nada.
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["persistencia"] = "sqlite",
});

var dataDir = builder.Configuration["Financemax:DataDir"]
    ?? Environment.GetEnvironmentVariable("FINANCEMAX_DATA_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "data");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Financemax:DataDir"] = dataDir,
    ["Financemax:BusinessId"] = businessId,
});

builder.Host.UseSerilog((_, loggerConfig) =>
{
    loggerConfig
        .MinimumLevel.Is(LogEventLevel.Information)
        .Enrich.WithProperty("servico", "financemax-api")
        .WriteTo.Console();
});

// 0.0.0.0:8080 — bind de container. F1: docker-compose.yml publica essa porta ("ports:") pra
// acesso local/VM direto (cloudflared ainda não existe no compose). Plano de produção
// (ARQUITETURA.md §4) é remover "ports:" e falar só via Cloudflare Tunnel — isso é F2.
// FINANCEMAX_PORT sobrepõe em dev local.
var porta = int.TryParse(Environment.GetEnvironmentVariable("FINANCEMAX_PORT"), out var portaEnv) ? portaEnv : 8080;
builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Any, porta));

var registry = FinancemaxHost.RegistrarModulos(builder.Services, CamadaExecucao.Nuvem, builder.Configuration, businessId);

var app = builder.Build();

app.UseMiddleware<StubAuthMiddleware>(businessId);

var api = app.MapGroup("/api");
HealthEndpoints.Mapear(api, iniciadoEm);

// Contrato IModuleEndpoints — o Host só ENUMERA, nunca conhece rota concreta de módulo nenhum
// (mesma regra de ouro do SistemaXHost/Host.Desktop).
foreach (var modulo in registry.ModulosAdicionados.OfType<IModuleEndpoints>())
{
    modulo.MapearEndpoints(api);
}

await app.StartAsync();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("financemax-api no ar em 0.0.0.0:{Porta} — businessId={BusinessId} — dataDir={DataDir}", porta, businessId, dataDir);

// Bootstrap do domínio Bancário (contas/formas de pagamento padrão) — IDEMPOTENTE, roda em todo
// boot, mesmo racional do Host.Desktop: sem isso a tela Bancário nasce vazia e fato_recebiveis cai
// sempre no fallback conservador por falta de FormaDePagamento pra resolver.
await FinanceiroBootstrapSeeder.SemearAsync(app.Services, businessId);
logger.LogInformation("Bootstrap de contas/formas de pagamento aplicado (idempotente).");

await app.WaitForShutdownAsync();

// Necessário para o WebApplicationFactory<Program> dos testes de integração (se/quando existirem)
// enxergar a classe de entrada.
public partial class Program;
