using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaX.Modules.Identidade.Application.Endpoints;

namespace SistemaX.Financemax.Api.Tests;

/// <summary>
/// Testes de integração ponta a ponta do <c>POST /api/financeiro/bancario/simular-emprestimo</c> —
/// TestServer in-process com o <c>Financemax.Api</c> completo (mesma fixture de
/// <see cref="AuthEndpointsTests"/>). O motor puro (PMT/viabilidade) e a orquestração do serviço já
/// têm cobertura unitária exaustiva em <c>SistemaX.Modules.Financeiro.Tests</c>; o que falta e este
/// arquivo cobre é a borda HTTP que só é observável com o pipeline real: 401 sem token, 403 com
/// token válido mas sem <c>Financeiro:Ver</c>, 200 com a permissão, e o SHAPE exato do JSON
/// (camelCase, campos aninhados de <c>viabilidade</c>) — nenhum teste unitário exercita a
/// serialização do endpoint minimal API.
/// </summary>
[Collection("financemax-api")]
public sealed class FinanceiroBancarioEndpointsTests(FinancemaxApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Rota = "/api/financeiro/bancario/simular-emprestimo";

    private static readonly object CorpoValido = new
    {
        valorCentavos = 10_000_00L,
        taxaJurosMensalBps = 200,
        prazoMeses = 12,
    };

    private HttpClient NovoCliente() => factory.CreateClient();

    private async Task<TokensResponse> LoginComoAdminAsync(HttpClient cliente)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(FinancemaxApiFactory.AdminEmail, FinancemaxApiFactory.AdminSenhaInicial), Json);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<TokensResponse>(Json))!;
    }

    private static void AutenticarCom(HttpClient cliente, string accessToken)
        => cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>Cria e loga um usuário num papel sem <c>Financeiro:Ver</c> por padrão (Operator —
    /// ver <c>PermissoesPadraoPorPapel</c>) usando o admin já autenticado da fixture.</summary>
    private async Task<TokensResponse> CriarELogarOperatorSemFinanceiroAsync()
    {
        var clienteAdmin = NovoCliente();
        var parAdmin = await LoginComoAdminAsync(clienteAdmin);
        AutenticarCom(clienteAdmin, parAdmin.AccessToken);

        var email = $"operator-emprestimo-{Guid.NewGuid():N}@exemplo.com";
        const string senha = "SenhaForteOperator!42";
        var respostaCriacao = await clienteAdmin.PostAsJsonAsync("/api/usuarios", new CriarUsuarioRequest("Sem Financeiro", email, senha, "Operator"));
        Assert.Equal(HttpStatusCode.Created, respostaCriacao.StatusCode);

        var clienteOperator = NovoCliente();
        var loginOperator = await clienteOperator.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, senha), Json);
        Assert.Equal(HttpStatusCode.OK, loginOperator.StatusCode);
        return (await loginOperator.Content.ReadFromJsonAsync<TokensResponse>(Json))!;
    }

    [Fact]
    public async Task Sem_token_devolve_401()
    {
        var cliente = NovoCliente();
        var resposta = await cliente.PostAsJsonAsync(Rota, CorpoValido, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Com_token_valido_mas_sem_permissao_FinanceiroVer_devolve_403()
    {
        var parOperator = await CriarELogarOperatorSemFinanceiroAsync();
        var cliente = NovoCliente();
        AutenticarCom(cliente, parOperator.AccessToken);

        var resposta = await cliente.PostAsJsonAsync(Rota, CorpoValido, Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Com_permissao_FinanceiroVer_devolve_200_com_o_shape_exato_do_json()
    {
        var cliente = NovoCliente();
        var par = await LoginComoAdminAsync(cliente);
        AutenticarCom(cliente, par.AccessToken);

        var resposta = await cliente.PostAsJsonAsync(Rota, CorpoValido, Json);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = (await resposta.Content.ReadFromJsonAsync<SimulacaoResponse>(Json))!;

        // Taxa 2%/mês, prazo 12 -> parcela ≈ R$945,60 (mesmo exemplo clássico da suíte unitária de
        // SimulacaoDeEmprestimoTests) — confirma que o corpo desserializou os campos certos, não só
        // que o shape "parece" certo.
        Assert.Equal(945_60, corpo.ParcelaCentavos);
        Assert.True(corpo.CustoTotalCentavos > 0);
        Assert.True(corpo.JurosTotalCentavos >= 0);
        Assert.True(corpo.TaxaEfetivaAnualBps > 0);

        Assert.NotNull(corpo.Viabilidade);
        Assert.Contains(corpo.Viabilidade!.Veredito, new[] { "viavel", "apertado", "inviavel" });
        Assert.False(string.IsNullOrWhiteSpace(corpo.Viabilidade.Motivo));
        Assert.True(double.IsFinite(corpo.Viabilidade.ParcelaVsFolgaPercent)); // nunca Infinity/NaN no wire
    }

    [Fact]
    public async Task Corpo_invalido_valor_zero_devolve_erro_de_validacao_400()
    {
        var cliente = NovoCliente();
        var par = await LoginComoAdminAsync(cliente);
        AutenticarCom(cliente, par.AccessToken);

        var resposta = await cliente.PostAsJsonAsync(Rota, new { valorCentavos = 0L, taxaJurosMensalBps = 200, prazoMeses = 12 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private sealed record SimulacaoResponse(
        long ParcelaCentavos, long CustoTotalCentavos, long JurosTotalCentavos, int TaxaEfetivaAnualBps,
        ViabilidadeResponse? Viabilidade);

    private sealed record ViabilidadeResponse(string Veredito, string Motivo, double ParcelaVsFolgaPercent, int? PaybackMeses);
}
