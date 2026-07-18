using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Application.Endpoints;

/// <summary>DTO de fio de <see cref="Usuario"/> — NUNCA inclui <c>SenhaHash</c> (§1 do escopo:
/// hash de senha não sai do servidor de jeito nenhum, nem para o próprio dono da conta).</summary>
public sealed record UsuarioDto(string Id, string Nome, string Email, string Papel, bool Ativo, bool MustChangePassword)
{
    public static UsuarioDto DeDominio(Usuario u) => new(u.Id, u.Nome, u.Email, u.Papel.ToString(), u.Ativo, u.MustChangePassword);
}

public sealed record LoginRequest(string Email, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record TokensResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiraEm, UsuarioDto Usuario)
{
    public static TokensResponse De(TokensEmitidosResultado r) =>
        new(r.AccessToken, r.RefreshToken, r.AccessTokenExpiraEm, UsuarioDto.DeDominio(r.Usuario));
}

public sealed record CriarUsuarioRequest(string Nome, string Email, string Senha, string Papel);

public sealed record AtualizarUsuarioRequest(string? Papel = null, bool? Ativo = null, string? NovaSenha = null);

/// <summary>
/// Terceiro <see cref="IModule"/> de Identidade — só rotas, mesmo molde de
/// <c>FinanceiroEndpointsModule</c>. <c>/auth/login</c>/<c>/auth/refresh</c>/<c>/auth/logout</c>
/// são <see cref="RouteHandlerBuilderExtensions.AllowAnonymous"/> — não fazem sentido atrás de um
/// Bearer válido (é exatamente o que estão emitindo/trocando/revogando). O restante do grupo
/// <c>/api</c> (mapeado por <c>Program.cs</c>) já exige autenticação por padrão — ver
/// <c>Financemax.Api/Program.cs</c>.
/// </summary>
public sealed class IdentidadeEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "identidade.endpoints";
    public string Nome => "Identidade — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["identidade"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        api.MapPost("/auth/login", async (LoginRequest req, ITenantsDeInstalacao tenants, LoginUseCase useCase, CancellationToken ct) =>
        {
            // businessId NUNCA vem do request (R1) — antes de autenticar não há sessão pra
            // resolvê-lo dela, então vem da config FIXA da instalação (mesmo port que os jobs de
            // background já usam para resolver "qual(is) tenant(s) esta instalação atende" —
            // ver ITenantsDeInstalacao/TenantsDeInstalacaoFixo no composition root).
            var businessId = (await tenants.ObterBusinessIdsAsync(ct).ConfigureAwait(false)).FirstOrDefault();
            if (businessId is null)
            {
                return new Error("identidade.login.instalacao_sem_negocio", "Instalação sem negócio configurado.").ParaRespostaHttp(StatusCodes.Status500InternalServerError);
            }

            var resultado = await useCase.ExecutarAsync(new LoginComando(businessId, req.Email, req.Senha), ct).ConfigureAwait(false);
            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(StatusCodeDoErroDeLogin(resultado.Erro))
                : Results.Ok(TokensResponse.De(resultado.Valor));
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-login");

        api.MapPost("/auth/refresh", async (RefreshRequest req, RefreshTokenUseCase useCase, CancellationToken ct) =>
        {
            var resultado = await useCase.ExecutarAsync(new RefreshTokenComando(req.RefreshToken), ct).ConfigureAwait(false);
            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(StatusCodes.Status401Unauthorized)
                : Results.Ok(TokensResponse.De(resultado.Valor));
        }).AllowAnonymous();

        api.MapPost("/auth/logout", async (LogoutRequest req, LogoutUseCase useCase, CancellationToken ct) =>
        {
            await useCase.ExecutarAsync(new LogoutComando(req.RefreshToken), ct).ConfigureAwait(false);
            return Results.NoContent();
        }).AllowAnonymous();

        api.MapPost("/usuarios", async (CriarUsuarioRequest req, HttpContext http, CriarUsuarioUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            if (!Enum.TryParse<Papel>(req.Papel, ignoreCase: true, out var papel))
            {
                return new Error("identidade.usuario.papel_invalido", $"Papel '{req.Papel}' desconhecido.").ParaRespostaHttp();
            }

            var resultado = await useCase.ExecutarAsync(new CriarUsuarioComando(businessId, req.Nome, req.Email, req.Senha, papel), ct)
                .ConfigureAwait(false);

            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(resultado.Erro.Codigo == "identidade.usuario.email_em_uso" ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity)
                : Results.Created($"/api/usuarios/{resultado.Valor.Id}", UsuarioDto.DeDominio(resultado.Valor));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        api.MapGet("/usuarios", async (HttpContext http, IUsuarioRepository repositorio, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var lista = await repositorio.ListarAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(UsuarioDto.DeDominio));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        api.MapPatch("/usuarios/{id}", async (string id, AtualizarUsuarioRequest req, HttpContext http, AtualizarUsuarioUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            Papel? novoPapel = null;
            if (req.Papel is not null)
            {
                if (!Enum.TryParse<Papel>(req.Papel, ignoreCase: true, out var papel))
                {
                    return new Error("identidade.usuario.papel_invalido", $"Papel '{req.Papel}' desconhecido.").ParaRespostaHttp();
                }
                novoPapel = papel;
            }

            var resultado = await useCase.ExecutarAsync(
                new AtualizarUsuarioComando(businessId, id, novoPapel, req.Ativo, req.NovaSenha), ct).ConfigureAwait(false);

            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(StatusCodeDoErroDeAtualizacao(resultado.Erro))
                : Results.Ok(UsuarioDto.DeDominio(resultado.Valor));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);
    }

    private static int StatusCodeDoErroDeLogin(Error erro) => erro.Codigo switch
    {
        "identidade.login.bloqueado_temporariamente" => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status401Unauthorized,
    };

    private static int StatusCodeDoErroDeAtualizacao(Error erro) => erro.Codigo switch
    {
        "identidade.usuario.nao_encontrado" => StatusCodes.Status404NotFound,
        "identidade.usuario.ultimo_founder" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
