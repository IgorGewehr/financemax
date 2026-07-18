using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Infrastructure.Seed;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.Seed;

/// <summary>
/// Prova a coerência entre o seed de bootstrap e o onboarding self-service
/// (<c>RegistrarUseCase</c>, first-run): SEM <c>FINANCEMAX_ADMIN_SENHA_INICIAL</c> setado (o caso de
/// PRODUÇÃO — o dono nunca seta essa env), o seed precisa ser NO-OP, senão o primeiro
/// <c>POST /api/auth/registrar</c> nunca encontraria zero usuários e o first-run nunca disparia.
/// COM a env setada (dev/teste — mesmo valor que <c>FinancemaxApiFactory</c> já usa), o seed
/// continua criando o admin Founder de sempre, byte a byte.
/// </summary>
public sealed class IdentidadeBootstrapSeederTests : IDisposable
{
    private const string BusinessId = "biz-seed-test";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_EMAIL_INICIAL", null);
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL", null);
    }

    private static IServiceProvider NovoProvider(IUsuarioRepository usuarios)
    {
        var services = new ServiceCollection();
        services.AddSingleton(usuarios);
        services.AddSingleton<IRelogio>(new FakeRelogio(Agora));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Sem_env_de_senha_inicial_o_seed_e_no_op_deixando_zero_usuarios()
    {
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_EMAIL_INICIAL", null);
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL", null);

        var usuarios = new InMemoryUsuarioRepository();
        var provider = NovoProvider(usuarios);

        await IdentidadeBootstrapSeeder.SemearAsync(provider, BusinessId);

        var lista = await usuarios.ListarAsync(BusinessId);
        Assert.Empty(lista);
    }

    [Fact]
    public async Task Com_env_de_senha_inicial_o_seed_cria_o_founder_de_sempre()
    {
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_EMAIL_INICIAL", "admin@teste.local");
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL", "SenhaConhecidaForte!1");

        var usuarios = new InMemoryUsuarioRepository();
        var provider = NovoProvider(usuarios);

        await IdentidadeBootstrapSeeder.SemearAsync(provider, BusinessId);

        var lista = await usuarios.ListarAsync(BusinessId);
        Assert.Single(lista);
        Assert.Equal("admin@teste.local", lista[0].Email);
        Assert.Equal(Papel.Founder, lista[0].Papel);
        Assert.True(lista[0].MustChangePassword);
    }

    [Fact]
    public async Task E_idempotente_nao_recria_se_ja_existe_algum_usuario()
    {
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_EMAIL_INICIAL", "admin@teste.local");
        Environment.SetEnvironmentVariable("FINANCEMAX_ADMIN_SENHA_INICIAL", "SenhaConhecidaForte!1");

        var usuarios = new InMemoryUsuarioRepository();
        var provider = NovoProvider(usuarios);

        await IdentidadeBootstrapSeeder.SemearAsync(provider, BusinessId);
        await IdentidadeBootstrapSeeder.SemearAsync(provider, BusinessId);

        var lista = await usuarios.ListarAsync(BusinessId);
        Assert.Single(lista);
    }
}
