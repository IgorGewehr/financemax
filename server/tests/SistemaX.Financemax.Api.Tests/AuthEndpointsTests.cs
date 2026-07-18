using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.Endpoints;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Financemax.Api.Tests;

/// <summary>
/// Testes de integração ponta a ponta do pipeline de auth REAL (F2) — TestServer in-process com o
/// <c>Financemax.Api</c> completo: JWT emitido/validado de verdade, <c>SessaoClaimsMiddleware</c>,
/// <c>RequerPermissao</c> (RBAC), rate limiter. Cobre exatamente a lista do escopo F2 que só é
/// observável na borda HTTP (o resto — regras de negócio dos casos de uso — já tem cobertura
/// unitária em <c>SistemaX.Modules.Identidade.Tests</c>).
/// </summary>
[Collection("financemax-api")]
public sealed class AuthEndpointsTests(FinancemaxApiFactory factory)
{
    // O servidor serializa em camelCase (Web defaults do minimal API); HttpClient.ReadFromJsonAsync
    // SEM opções explícitas usa JsonSerializerOptions.Default (case-sensitive) — sem isto, os
    // records de resposta desserializariam com todos os campos nulos.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient NovoCliente() => factory.CreateClient();

    private async Task<TokensResponse> LoginComoAdminAsync(HttpClient cliente)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(FinancemaxApiFactory.AdminEmail, FinancemaxApiFactory.AdminSenhaInicial), Json);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<TokensResponse>(Json))!;
    }

    private static void AutenticarCom(HttpClient cliente, string accessToken)
        => cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    [Fact]
    public async Task Endpoint_protegido_sem_token_devolve_401()
    {
        var cliente = NovoCliente();
        var resposta = await cliente.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_com_credenciais_do_admin_semeado_funciona()
    {
        var cliente = NovoCliente();
        var tokens = await LoginComoAdminAsync(cliente);

        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshToken);
        Assert.Equal(FinancemaxApiFactory.AdminEmail, tokens.Usuario.Email);
        Assert.Equal("Founder", tokens.Usuario.Papel);
        Assert.True(tokens.Usuario.MustChangePassword); // seed nasce exigindo troca de senha
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_401_com_mensagem_generica()
    {
        var cliente = NovoCliente();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(FinancemaxApiFactory.AdminEmail, "senha-totalmente-errada"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_com_email_inexistente_devolve_o_MESMO_401_generico_nao_vaza_existencia()
    {
        var cliente = NovoCliente();

        var respostaSenhaErrada = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(FinancemaxApiFactory.AdminEmail, "senha-errada"), Json);
        var respostaEmailInexistente = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest("ninguem@exemplo.com", "senha-errada"), Json);

        var corpoSenhaErrada = await respostaSenhaErrada.Content.ReadFromJsonAsync<ErroResponse>(Json);
        var corpoEmailInexistente = await respostaEmailInexistente.Content.ReadFromJsonAsync<ErroResponse>(Json);

        Assert.Equal(HttpStatusCode.Unauthorized, respostaEmailInexistente.StatusCode);
        Assert.Equal(corpoSenhaErrada!.Codigo, corpoEmailInexistente!.Codigo);
    }

    [Fact]
    public async Task Refresh_rotaciona_e_o_token_antigo_para_de_funcionar()
    {
        var cliente = NovoCliente();
        var primeiroPar = await LoginComoAdminAsync(cliente);

        var respostaRefresh = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(primeiroPar.RefreshToken), Json);
        Assert.Equal(HttpStatusCode.OK, respostaRefresh.StatusCode);
        var novoPar = (await respostaRefresh.Content.ReadFromJsonAsync<TokensResponse>(Json))!;
        Assert.NotEqual(primeiroPar.RefreshToken, novoPar.RefreshToken);

        // Reapresentar o token ANTIGO (já rotacionado) — reuso detectado, deve falhar.
        var respostaReuso = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(primeiroPar.RefreshToken), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, respostaReuso.StatusCode);
    }

    [Fact]
    public async Task Logout_revoga_o_refresh_apresentado()
    {
        var cliente = NovoCliente();
        var par = await LoginComoAdminAsync(cliente);

        var respostaLogout = await cliente.PostAsJsonAsync("/api/auth/logout", new LogoutRequest(par.RefreshToken), Json);
        Assert.Equal(HttpStatusCode.NoContent, respostaLogout.StatusCode);

        var respostaRefreshDepois = await cliente.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(par.RefreshToken), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, respostaRefreshDepois.StatusCode);
    }

    [Fact]
    public async Task Token_JWT_expirado_e_rejeitado_com_401()
    {
        var cliente = NovoCliente();

        // Fabrica um access token JÁ EXPIRADO com a MESMA chave que o servidor de teste usa
        // (FinancemaxApiFactory.JwtSecret) — não passa pelo endpoint de login de propósito: o
        // objetivo é testar a VALIDAÇÃO de expiração do middleware, não a emissão. Emite com
        // "agora" 20 minutos no PASSADO (validade padrão de 15 min) em vez de duração negativa —
        // JwtSecurityToken exige notBefore < expires, uma duração negativa violaria isso na hora
        // de construir o token, antes mesmo de chegar ao middleware que queremos testar.
        var jwtExpirado = new JwtOptions(FinancemaxApiFactory.JwtSecret);
        var geradorExpirado = new GeradorDeTokens(jwtExpirado);
        var usuarioFake = Usuario.Criar(FinancemaxApiFactory.BusinessId, "Fake", "fake@exemplo.com", "hash", Papel.Founder, DateTimeOffset.UtcNow).Valor;
        var (tokenExpirado, _) = geradorExpirado.GerarAccessToken(usuarioFake, DateTimeOffset.UtcNow.AddMinutes(-20));

        AutenticarCom(cliente, tokenExpirado);
        var resposta = await cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task RBAC_viewer_recebe_403_em_rota_de_gerenciar_usuarios_founder_recebe_sucesso()
    {
        var clienteAdmin = NovoCliente();
        var parAdmin = await LoginComoAdminAsync(clienteAdmin);
        AutenticarCom(clienteAdmin, parAdmin.AccessToken);

        // Founder cria um usuário Viewer.
        var emailViewer = $"viewer-{Guid.NewGuid():N}@exemplo.com";
        var senhaViewer = "SenhaForteDoViewer!42";
        var respostaCriacao = await clienteAdmin.PostAsJsonAsync("/api/usuarios", new CriarUsuarioRequest("Sócio Leitor", emailViewer, senhaViewer, "Viewer"));
        Assert.Equal(HttpStatusCode.Created, respostaCriacao.StatusCode);

        // Founder tentando de novo (mesma rota) — deve continuar 201/200 (permissão de founder).
        var respostaCriacao2 = await clienteAdmin.PostAsJsonAsync(
            "/api/usuarios", new CriarUsuarioRequest("Outro", $"outro-{Guid.NewGuid():N}@exemplo.com", "OutraSenhaForte!7", "Operator"));
        Assert.Equal(HttpStatusCode.Created, respostaCriacao2.StatusCode);

        // Login como o Viewer recém-criado.
        var clienteViewer = NovoCliente();
        var loginViewer = await clienteViewer.PostAsJsonAsync("/api/auth/login", new LoginRequest(emailViewer, senhaViewer), Json);
        Assert.Equal(HttpStatusCode.OK, loginViewer.StatusCode);
        var parViewer = (await loginViewer.Content.ReadFromJsonAsync<TokensResponse>(Json))!;
        AutenticarCom(clienteViewer, parViewer.AccessToken);

        // Viewer não tem Configuracoes:GerenciarUsuarios — 403, não 401 (está autenticado, só sem permissão).
        var respostaViewer = await clienteViewer.PostAsJsonAsync(
            "/api/usuarios", new CriarUsuarioRequest("Mais Um", $"maisum-{Guid.NewGuid():N}@exemplo.com", "SenhaForte!789", "Viewer"));
        Assert.Equal(HttpStatusCode.Forbidden, respostaViewer.StatusCode);
    }

    [Fact]
    public async Task Criar_usuario_com_senha_fraca_e_recusado()
    {
        var cliente = NovoCliente();
        var par = await LoginComoAdminAsync(cliente);
        AutenticarCom(cliente, par.AccessToken);

        var resposta = await cliente.PostAsJsonAsync(
            "/api/usuarios", new CriarUsuarioRequest("Fulano", $"fraco-{Guid.NewGuid():N}@exemplo.com", "12345678", "Viewer"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Apos_N_tentativas_de_login_com_senha_errada_o_endpoint_devolve_429()
    {
        var cliente = NovoCliente();
        var emailAlvo = $"lockout-{Guid.NewGuid():N}@exemplo.com";

        // Precisa existir um usuário de verdade pro lockout por e-mail entrar em jogo (login
        // contra e-mail inexistente também conta falha, mas criamos um usuário dedicado pra não
        // interferir no estado de outros testes que também erram a senha do admin).
        var clienteAdmin = NovoCliente();
        var parAdmin = await LoginComoAdminAsync(clienteAdmin);
        AutenticarCom(clienteAdmin, parAdmin.AccessToken);
        await clienteAdmin.PostAsJsonAsync("/api/usuarios", new CriarUsuarioRequest("Alvo do Lockout", emailAlvo, "SenhaForteAlvo!99", "Viewer"));

        HttpResponseMessage? ultimaResposta = null;
        for (var i = 0; i < 6; i++)
        {
            ultimaResposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(emailAlvo, "senha-errada-de-proposito"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultimaResposta!.StatusCode);
    }
}

/// <summary>Espelha o formato de erro de <c>ErrorHttpExtensions.ParaRespostaHttp</c> — só pra
/// desserializar em teste, não é o DTO real da API.</summary>
internal sealed record ErroResponse(string Codigo, string Mensagem);
