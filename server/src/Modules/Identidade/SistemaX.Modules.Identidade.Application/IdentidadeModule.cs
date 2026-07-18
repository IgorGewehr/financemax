using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;

namespace SistemaX.Modules.Identidade.Application;

/// <summary>
/// Módulo Identidade — login e-mail+senha multi-usuário + gestão de usuários (§F2 do MVP-ESCOPO,
/// ARQUITETURA.md §3). Mesmo desenho de dois <c>IModule</c> que o Financeiro
/// (<c>FinanceiroModule</c>/<c>FinanceiroInfrastructureModule</c>): este registra os CASOS DE USO;
/// <c>IdentidadeInfrastructureModule</c> (Infrastructure) registra os adapters concretos dos
/// ports. <see cref="JwtOptions"/> é registrado pelo HOST (<c>Financemax.Api</c>), não aqui — a
/// chave de assinatura vem de config/env do processo (nunca hardcoded), o módulo só CONSOME.
/// </summary>
public sealed class IdentidadeModule : IModule
{
    public string Codigo => "identidade";
    public string Nome => "Identidade";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddSingleton<GeradorDeTokens>();

        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddScoped<CriarUsuarioUseCase>();
        services.AddScoped<AtualizarUsuarioUseCase>();
        services.AddScoped<RegistrarUseCase>();
        services.AddScoped<CriarConviteUseCase>();
        services.AddScoped<RevogarConviteUseCase>();
        services.AddScoped<ConsultarConvitePorTokenUseCase>();
    }
}
