using System.Net;
using Financemax.Api.Auth;
using Financemax.Api.Bridge;
using Financemax.Api.Composition;
using Serilog;
using Serilog.Events;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Infrastructure.Seed;
using SistemaX.Modules.Identidade.Infrastructure.Seed;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// financemax F2 — servidor Financeiro headless + auth e-mail+senha multi-usuário ONLINE
// (MVP-ESCOPO.md, ARQUITETURA.md §3). Molde: SistemaX.Host.Desktop/Program.cs, reduzido ao que um
// container Linux precisa: sem Velopack (updater), sem Photino (janela), sem boot-token/PIN. O
// antigo StubAuthMiddleware (F1, businessId+papel fixos) foi SUBSTITUÍDO por JWT real
// (JwtAuthSetup.AddIdentidadeAuth + SessaoClaimsMiddleware) — businessId continua fixo por
// instalação (single-tenant, R1 nunca do request), mas papel/usuarioId agora vêm do TOKEN
// validado por usuário de verdade. Kestrel escuta 0.0.0.0:8080 dentro do container. O
// docker-compose.yml ainda PUBLICA essa porta pra fora ("ports:") para acesso local/LAN direto;
// o acesso remoto seguro é via Cloudflare Tunnel (profile opcional "tunnel", outbound-only) — ver
// docker-compose.yml e README.md "Acesso remoto".
// ─────────────────────────────────────────────────────────────────────────────────────────────

var iniciadoEm = DateTimeOffset.UtcNow;

var builder = WebApplication.CreateBuilder(args);

// businessId fixo desta instalação (single-tenant por design do MVP — ver ITenantsDeInstalacao/
// FinancemaxHost; SessaoClaimsMiddleware NUNCA lê businessId do request, só do token validado,
// que por sua vez só existe porque LoginUseCase o resolveu daqui). Financemax:BusinessId
// (appsettings/env FINANCEMAX__BUSINESSID) com fallback de dev.
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

// 0.0.0.0:8080 — bind de container. docker-compose.yml continua publicando essa porta ("ports:")
// pra acesso local/LAN direto; o serviço `cloudflared` (profile opcional "tunnel") ruteia pra ela
// via rede interna do compose pra expor sem abrir porta na borda — ver docker-compose.yml.
// FINANCEMAX_PORT sobrepõe em dev local.
var porta = int.TryParse(Environment.GetEnvironmentVariable("FINANCEMAX_PORT"), out var portaEnv) ? portaEnv : 8080;
builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Any, porta));

// JWT — F2. Falha rápido no boot se FINANCEMAX_JWT_SECRET não estiver configurado (ver
// JwtAuthSetup.ResolverChaveSecreta): um servidor multi-usuário ONLINE sem segredo de assinatura
// forte configurado não é um estado seguro para subir, nem em dev (o .env local já tem um valor
// de desenvolvimento — ver .env.example).
var jwtOptions = JwtAuthSetup.ResolverOptions(builder.Configuration);
builder.Services.AddIdentidadeAuth(jwtOptions);

var registry = FinancemaxHost.RegistrarModulos(builder.Services, CamadaExecucao.Nuvem, builder.Configuration, businessId);

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<SessaoClaimsMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

// Grupo /api EXIGE autenticação por padrão (401 sem token válido) — as poucas rotas que
// precisam ser públicas (health check, login, refresh, logout) chamam .AllowAnonymous()
// explicitamente na hora de mapear (ver HealthEndpoints.Mapear / IdentidadeEndpointsModule).
var api = app.MapGroup("/api").RequireAuthorization();
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

// F2 — bootstrap do usuário founder inicial (§6 do escopo): sem isto, uma instalação nova não
// tem NENHUM usuário e ninguém consegue logar pela primeira vez (só quem já está logado pode
// criar outro usuário via POST /api/usuarios).
await IdentidadeBootstrapSeeder.SemearAsync(app.Services, businessId);
logger.LogInformation("Bootstrap de usuário administrador aplicado (idempotente).");

await app.WaitForShutdownAsync();

// Necessário para o WebApplicationFactory<Program> dos testes de integração (se/quando existirem)
// enxergar a classe de entrada.
public partial class Program;
