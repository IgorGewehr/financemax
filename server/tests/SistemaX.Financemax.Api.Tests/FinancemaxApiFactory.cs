using Microsoft.AspNetCore.Mvc.Testing;

namespace SistemaX.Financemax.Api.Tests;

/// <summary>
/// Fixture de integração — sobe o <c>Financemax.Api</c> REAL (pipeline completo: JWT real,
/// SessaoClaimsMiddleware, RequerPermissao, rate limiter, migrações SQLite, seed do admin) num
/// <c>TestServer</c> in-process, banco SQLite isolado num diretório temporário por instância de
/// fixture (uma por classe de teste via <see cref="AuthCollection"/> — evita corrida entre
/// variáveis de ambiente globais quando xUnit paraleliza classes).
/// </summary>
public sealed class FinancemaxApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "segredo-de-teste-nao-usar-em-producao-1234567890";
    public const string BusinessId = "test-tenant";
    public const string AdminEmail = "admin@test.local";
    public const string AdminSenhaInicial = "AdminSenhaForte!123";

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"financemax-api-tests-{Guid.NewGuid():N}");

    public FinancemaxApiFactory()
    {
        // Program.cs lê estas variáveis de ambiente ANTES de qualquer hook de configuração do
        // WebApplicationFactory (top-level statements, sem Startup/CreateHostBuilder) — setar aqui,
        // no construtor da fixture, GARANTE que rodam antes do primeiro acesso a Server/CreateClient.
        Environment.SetEnvironmentVariable("FINANCEMAX_JWT_SECRET", JwtSecret);
        Environment.SetEnvironmentVariable("FINANCEMAX_DATA_DIR", _dataDir);
        Environment.SetEnvironmentVariable("Financemax__BusinessId", BusinessId);
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_EMAIL_INICIAL", AdminEmail);
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL", AdminSenhaInicial);

        // O TestServer in-process não popula Connection.RemoteIpAddress — toda requisição da
        // suíte inteira cairia na MESMA partição de IP do rate limiter ("sem-ip"). Sem isto, os
        // ~16 POSTs de login espalhados pelos testes estourariam o teto de produção (10/min) e
        // produziriam 429 espúrio em testes que não têm nada a ver com rate limit. O teste que
        // EXERCITA 429 de propósito (lockout progressivo) usa o mecanismo de aplicação
        // (ITentativaLoginStore, por e-mail), não este limiter de borda — os dois são
        // independentes por design (ver JwtAuthSetup).
        Environment.SetEnvironmentVariable("FINANCEMAX_LOGIN_RATE_LIMIT_PERMIT", "100000");
    }

    public Task InitializeAsync()
    {
        // Força o boot completo (migrações + seed do admin) antes do primeiro teste rodar —
        // CreateClient() já dispara isso, mas fazer explicitamente aqui deixa o custo do primeiro
        // boot (Argon2id do seed + migrações) fora do tempo do primeiro [Fact].
        _ = Server;
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* best-effort — diretório temporário */ }
    }
}
