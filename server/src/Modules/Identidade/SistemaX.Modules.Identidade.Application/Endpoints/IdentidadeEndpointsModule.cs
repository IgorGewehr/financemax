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
using SistemaX.Modules.Identidade.Domain.Convites;
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

public sealed record RegistrarRequest(string Nome, string Email, string Senha, string? ConviteToken = null);

public sealed record CriarConviteRequest(string Email, string Papel);

/// <summary>Nunca inclui <c>TokenHash</c>/<c>CriadoPorUsuarioId</c> — só o que a UI de "convites
/// pendentes" precisa mostrar.</summary>
public sealed record ConviteDto(string Id, string Email, string Papel, DateTimeOffset CriadoEm, DateTimeOffset ExpiraEm)
{
    public static ConviteDto DeDominio(Convite c) => new(c.Id, c.Email, c.Papel.ToString(), c.CriadoEm, c.ExpiraEm);
}

/// <summary>O <see cref="Token"/> BRUTO só aparece aqui — única resposta HTTP que o carrega (ver
/// <c>ConviteEmitido</c>). O front monta <c>/aceitar-convite?token=...</c> e repassa ao convidado.</summary>
public sealed record ConviteEmitidoResponse(string Token, string Email, string Papel, DateTimeOffset ExpiraEm);

public sealed record ConvitePreviewResponse(bool Valido, string? Email, string? Papel, string? Motivo);

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

        // Onboarding self-service — as DUAS portas de entrada (1º dono / convidado) do mesmo
        // endpoint, ver RegistrarUseCase. AllowAnonymous pela MESMA razão de /auth/login (não faz
        // sentido exigir um Bearer válido pra criar a PRIMEIRA conta); mesmo rate limit por IP de
        // /auth/login — é igualmente um endpoint público que cria estado a partir de input não
        // autenticado.
        api.MapPost("/auth/registrar", async (RegistrarRequest req, ITenantsDeInstalacao tenants, RegistrarUseCase useCase, CancellationToken ct) =>
        {
            var businessId = (await tenants.ObterBusinessIdsAsync(ct).ConfigureAwait(false)).FirstOrDefault();
            if (businessId is null)
            {
                return new Error("identidade.registrar.instalacao_sem_negocio", "Instalação sem negócio configurado.").ParaRespostaHttp(StatusCodes.Status500InternalServerError);
            }

            var resultado = await useCase.ExecutarAsync(new RegistrarComando(businessId, req.Nome, req.Email, req.Senha, req.ConviteToken), ct).ConfigureAwait(false);
            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(StatusCodeDoErroDeRegistro(resultado.Erro))
                : Results.Ok(TokensResponse.De(resultado.Valor));
        })
        .AllowAnonymous()
        .RequireRateLimiting("auth-login");

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

        // Emite o convite — mesmo guard de permissão de POST /usuarios (só founder/admin). O
        // TokenBruto só sai aqui (ConviteEmitidoResponse.Token); o front monta o link
        // /aceitar-convite?token=... e repassa ao convidado por fora deste sistema (e-mail, chat).
        api.MapPost("/convites", async (CriarConviteRequest req, HttpContext http, CriarConviteUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var criadoPorUsuarioId = http.ObterUsuarioId();

            if (!Enum.TryParse<Papel>(req.Papel, ignoreCase: true, out var papel))
            {
                return new Error("identidade.convite.papel_invalido", $"Papel '{req.Papel}' desconhecido.").ParaRespostaHttp();
            }

            var resultado = await useCase.ExecutarAsync(new CriarConviteComando(businessId, criadoPorUsuarioId, req.Email, papel), ct).ConfigureAwait(false);
            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(resultado.Erro.Codigo == "identidade.convite.email_ja_cadastrado" ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity)
                : Results.Created(
                    $"/api/convites/{resultado.Valor.Convite.Id}",
                    new ConviteEmitidoResponse(resultado.Valor.TokenBruto, resultado.Valor.Convite.Email, resultado.Valor.Convite.Papel.ToString(), resultado.Valor.Convite.ExpiraEm));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        api.MapGet("/convites", async (HttpContext http, IConviteRepository repositorio, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var lista = await repositorio.ListarPendentesAsync(businessId, ct).ConfigureAwait(false);
            return Results.Ok(lista.Select(ConviteDto.DeDominio));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        api.MapPost("/convites/{id}/revogar", async (string id, HttpContext http, RevogarConviteUseCase useCase, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var resultado = await useCase.ExecutarAsync(new RevogarConviteComando(businessId, id), ct).ConfigureAwait(false);
            return resultado.Falha
                ? resultado.Erro.ParaRespostaHttp(resultado.Erro.Codigo == "identidade.convite.nao_encontrado" ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity)
                : Results.NoContent();
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        // ANÔNIMO de propósito — o convidado ainda não tem sessão nenhuma quando abre o link
        // /aceitar-convite?token=...; só devolve email+papel do convite (nunca CriadoPorUsuarioId/
        // TokenHash) pra pré-preencher o formulário antes de POST /auth/registrar.
        api.MapGet("/convites/{token}", async (string token, ConsultarConvitePorTokenUseCase useCase, CancellationToken ct) =>
        {
            var resultado = await useCase.ExecutarAsync(token, ct).ConfigureAwait(false);
            return Results.Ok(new ConvitePreviewResponse(resultado.Valido, resultado.Email, resultado.Papel, resultado.Motivo));
        }).AllowAnonymous();
    }

    private static int StatusCodeDoErroDeLogin(Error erro) => erro.Codigo switch
    {
        "identidade.login.bloqueado_temporariamente" => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status401Unauthorized,
    };

    private static int StatusCodeDoErroDeRegistro(Error erro) => erro.Codigo switch
    {
        "identidade.registrar.email_em_uso" => StatusCodes.Status409Conflict,
        "identidade.registrar.convite_obrigatorio" => StatusCodes.Status403Forbidden,
        "identidade.registrar.convite_invalido" => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    private static int StatusCodeDoErroDeAtualizacao(Error erro) => erro.Codigo switch
    {
        "identidade.usuario.nao_encontrado" => StatusCodes.Status404NotFound,
        "identidade.usuario.ultimo_founder" => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
