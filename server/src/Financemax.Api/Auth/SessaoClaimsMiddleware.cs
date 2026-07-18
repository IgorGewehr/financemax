using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.Auth;

namespace Financemax.Api.Auth;

/// <summary>
/// SUBSTITUI o antigo <c>StubAuthMiddleware</c> (F1) — a PONTE entre o
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> que <c>UseAuthentication()</c>/JwtBearer já
/// validou (assinatura + emissor + audiência + expiração) e o <c>HttpContext.Items</c> que
/// <c>SessaoHttpContextExtensions</c>/<c>RequerPermissao</c> (RBAC do Financeiro) já esperavam
/// desde a F1 — ZERO mudança nos endpoints de módulo, só a ORIGEM do businessId/papel/usuarioId
/// deixa de ser fixa e passa a vir do token validado.
///
/// Roda DEPOIS de <c>UseAuthentication()</c> (para <c>HttpContext.User</c> já estar populado) e
/// ANTES de <c>UseAuthorization()</c>/dos endpoints. Se a requisição não está autenticada
/// (rota <c>AllowAnonymous</c> sem token, ou token ausente/inválido), simplesmente não popula os
/// Items — <c>RequerPermissao</c> reage com 403 "papel_desconhecido"; a ausência de token em rota
/// que EXIGE auth já foi barrada antes disso, com 401, pelo próprio <c>UseAuthorization()</c>
/// (grupo `/api` é <c>RequireAuthorization()</c> por padrão — ver Program.cs).
/// </summary>
public sealed class SessaoClaimsMiddleware(RequestDelegate proximo)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var usuario = http.User;
        if (usuario.Identity?.IsAuthenticated == true)
        {
            var businessId = usuario.FindFirst(JwtOptions.ClaimBusinessId)?.Value;
            var papel = usuario.FindFirst(JwtOptions.ClaimPapel)?.Value;
            var usuarioId = usuario.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(businessId)) http.Items[SessaoHttpContextExtensions.BusinessIdItemKey] = businessId;
            if (!string.IsNullOrEmpty(papel)) http.Items[SessaoHttpContextExtensions.PapelItemKey] = papel;
            if (!string.IsNullOrEmpty(usuarioId)) http.Items[SessaoHttpContextExtensions.UsuarioIdItemKey] = usuarioId;
        }

        await proximo(http).ConfigureAwait(false);
    }
}
