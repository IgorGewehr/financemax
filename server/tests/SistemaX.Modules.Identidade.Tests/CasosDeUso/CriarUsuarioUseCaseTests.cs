using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Auth;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Tests.Fakes;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class CriarUsuarioUseCaseTests
{
    private const string BusinessId = "biz-a";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Cria_usuario_com_senha_hasheada_nunca_em_claro()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(usuarios, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new CriarUsuarioComando(BusinessId, "Colega", "colega@exemplo.com", "S3nhaForte!22", Papel.Operator));

        Assert.True(resultado.Sucesso);
        Assert.NotEqual("S3nhaForte!22", resultado.Valor.SenhaHash);
        Assert.True(SenhaHasher.Verificar("S3nhaForte!22", resultado.Valor.SenhaHash));
    }

    [Fact]
    public async Task Recusa_email_duplicado_no_mesmo_negocio()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(usuarios, new FakeRelogio(Agora));
        await useCase.ExecutarAsync(new CriarUsuarioComando(BusinessId, "Colega", "colega@exemplo.com", "S3nhaForte!22", Papel.Operator));

        var segunda = await useCase.ExecutarAsync(new CriarUsuarioComando(BusinessId, "Outro", "colega@exemplo.com", "OutraSenha!99", Papel.Viewer));

        Assert.True(segunda.Falha);
        Assert.Equal("identidade.usuario.email_em_uso", segunda.Erro.Codigo);
    }

    [Fact]
    public async Task Recusa_senha_fraca()
    {
        var usuarios = new InMemoryUsuarioRepository();
        var useCase = new CriarUsuarioUseCase(usuarios, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new CriarUsuarioComando(BusinessId, "Colega", "colega@exemplo.com", "12345678", Papel.Operator));

        Assert.True(resultado.Falha);
    }
}
