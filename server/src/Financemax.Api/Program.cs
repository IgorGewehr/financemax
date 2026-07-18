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
// PIN (StubAuthMiddleware até a F2 trazer e-mail+senha real). Kestrel escuta 0.0.0.0:8080 —
// DENTRO do container isso é seguro (docker-compose.yml não publica a porta pra fora; o acesso de
// verdade em produção é via Cloudflare Tunnel, ARQUITETURA.md §4).
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

// 0.0.0.0:8080 — bind de container (ARQUITETURA.md §4: Kestrel sem "ports:" expostas no compose;
// só cloudflared fala com a API por fora). FINANCEMAX_PORT sobrepõe em dev local.
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
