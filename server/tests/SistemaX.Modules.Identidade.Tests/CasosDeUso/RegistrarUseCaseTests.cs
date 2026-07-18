using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class RegistrarUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    private static (RegistrarUseCase UseCase, InMemoryUsuarioRepository Usuarios, InMemoryConviteRepository Convites, FakeRelogio Relogio) NovoCenario()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var convites = new InMemoryConviteRepository();
        var refreshTokens = new InMemoryRefreshTokenRepository();
        var relogio = new FakeRelogio(Agora);
        var loginUseCase = new LoginUseCase(usuarios, refreshTokens, new InMemoryTentativaLoginStore(), new GeradorDeTokens(new JwtOptions("segredo-de-teste-1234567890123456")), relogio);
        var useCase = new RegistrarUseCase(usuarios, convites, loginUseCase, relogio);
        return (useCase, usuarios, convites, relogio);
    }

    [Fact]
    public async Task First_run_sem_usuarios_vira_founder_sem_exigir_convite()
    {
        var (useCase, usuarios, _, _) = NovoCenario();

        var resultado = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "S3nhaForte!22", ConviteToken: null));

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Founder, resultado.Valor.Usuario.Papel);
        Assert.False(resultado.Valor.Usuario.MustChangePassword);
        Assert.NotEmpty(resultado.Valor.AccessToken);
        Assert.NotEmpty(resultado.Valor.RefreshToken);

        var salvo = await usuarios.ObterPorEmailAsync(BusinessId, "dono@exemplo.com");
        Assert.NotNull(salvo);
        Assert.Equal(Papel.Founder, salvo!.Papel);
    }

    [Fact]
    public async Task Registrar_sem_token_com_usuarios_existentes_e_recusado()
    {
        var (useCase, _, _, _) = NovoCenario();
        await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "S3nhaForte!22"));

        var segundo = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Colega Sem Convite", "colega-sc@exemplo.com", "S3guraTotal!42", ConviteToken: null));

        Assert.True(segundo.Falha);
        Assert.Equal("identidade.registrar.convite_obrigatorio", segundo.Erro.Codigo);
    }

    [Fact]
    public async Task Registrar_com_convite_valido_cria_com_o_papel_do_convite_e_marca_aceito()
    {
        var (useCase, _, convites, relogio) = NovoCenario();
        await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "S3nhaForte!22"));

        var (bruto, hash) = ConviteTokenGerador.Gerar();
        var convite = Convite.Criar(BusinessId, "colega@exemplo.com", Papel.Manager, hash, "id-do-dono", relogio.Agora(), TimeSpan.FromDays(7)).Valor;
        await convites.SalvarAsync(convite);

        var resultado = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Colega", "colega@exemplo.com", "OutraSenhaForte!7", bruto));

        Assert.True(resultado.Sucesso);
        Assert.Equal(Papel.Manager, resultado.Valor.Usuario.Papel);

        var conviteAtualizado = await convites.ObterPorIdAsync(BusinessId, convite.Id);
        Assert.NotNull(conviteAtualizado!.AceitoEm);
        Assert.Equal(StatusConvite.Aceito, conviteAtualizado.Status(relogio.Agora()));
    }

    [Fact]
    public async Task Registrar_com_token_de_convite_expirado_e_recusado()
    {
        var (useCase, _, convites, relogio) = NovoCenario();
        await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "S3nhaForte!22"));

        var (bruto, hash) = ConviteTokenGerador.Gerar();
        var convite = Convite.Criar(BusinessId, "colega@exemplo.com", Papel.Manager, hash, "id-do-dono", relogio.Agora().AddDays(-8), TimeSpan.FromDays(7)).Valor;
        await convites.SalvarAsync(convite);

        var resultado = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Colega", "colega@exemplo.com", "OutraSenhaForte!7", bruto));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.registrar.convite_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Registrar_com_convite_ja_aceito_nao_pode_ser_reusado()
    {
        var (useCase, _, convites, relogio) = NovoCenario();
        await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "S3nhaForte!22"));

        var (bruto, hash) = ConviteTokenGerador.Gerar();
        var convite = Convite.Criar(BusinessId, "colega@exemplo.com", Papel.Manager, hash, "id-do-dono", relogio.Agora(), TimeSpan.FromDays(7)).Valor;
        await convites.SalvarAsync(convite);

        var primeiro = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Colega", "colega@exemplo.com", "OutraSenhaForte!7", bruto));
        Assert.True(primeiro.Sucesso);

        // Tentativa de reuso — o cadastro já existe, então o e-mail já duplicado é o motivo real;
        // com outro e-mail (mesmo token) o motivo passa a ser explicitamente "já aceito".
        var reuso = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Outra Pessoa", "outra-pessoa@exemplo.com", "MaisUmaSenha!8", bruto));

        Assert.True(reuso.Falha);
        Assert.Equal("identidade.registrar.convite_invalido", reuso.Erro.Codigo);
    }

    [Fact]
    public async Task Registrar_com_email_duplicado_e_recusado()
    {
        var (useCase, _, convites, relogio) = NovoCenario();
        await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "S3nhaForte!22"));

        var (bruto, hash) = ConviteTokenGerador.Gerar();
        var convite = Convite.Criar(BusinessId, "dono@exemplo.com", Papel.Manager, hash, "id-do-dono", relogio.Agora(), TimeSpan.FromDays(7)).Valor;
        await convites.SalvarAsync(convite);

        var resultado = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Outro Dono", "dono@exemplo.com", "OutraSenhaForte!7", bruto));

        Assert.True(resultado.Falha);
        Assert.Equal("identidade.registrar.email_em_uso", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task Registrar_com_senha_fraca_e_recusado()
    {
        var (useCase, _, _, _) = NovoCenario();

        var resultado = await useCase.ExecutarAsync(new RegistrarComando(BusinessId, "Dono", "dono@exemplo.com", "12345678"));

        Assert.True(resultado.Falha);
    }
}
