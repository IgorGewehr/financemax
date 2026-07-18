using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;

namespace Financemax.Api.Auth;

/// <summary>
/// PLACEHOLDER de autenticação — F1 é só o servidor headless rodando; auth de verdade (e-mail +
/// senha + JWT/refresh, ARQUITETURA.md §3) é a PRÓXIMA fatia (F2). Este middleware preenche os
/// mesmos <c>HttpContext.Items</c> que <c>SessaoHttpContextExtensions</c>/<c>RequerPermissao</c> já
/// esperam (<see cref="SessaoHttpContextExtensions.BusinessIdItemKey"/>/<c>PapelItemKey</c>/
/// <c>UsuarioIdItemKey</c>) — os endpoints do Financeiro (que já leem tenant/papel SÓ daqui, nunca
/// de query/corpo — R1) funcionam sem nenhuma mudança quando a F2 trocar este middleware pelo real.
///
/// Single-tenant fixo nesta fatia (mesmo desenho do Host.Desktop): o businessId vem de
/// <c>Financemax:BusinessId</c> (config/env), nunca do request — um cliente não pode forjar tenant
/// só porque a auth real ainda não existe. Papel fixo <c>Founder</c> (acesso total) até a F2 trazer
/// sessão por usuário real.
/// </summary>
public sealed class StubAuthMiddleware(RequestDelegate proximo, string businessId)
{
    public async Task InvokeAsync(HttpContext http)
    {
        http.Items[SessaoHttpContextExtensions.BusinessIdItemKey] = businessId;
        http.Items[SessaoHttpContextExtensions.PapelItemKey] = Papel.Founder.ToString();
        http.Items[SessaoHttpContextExtensions.UsuarioIdItemKey] = "stub-auth-f1";

        await proximo(http).ConfigureAwait(false);
    }
}
