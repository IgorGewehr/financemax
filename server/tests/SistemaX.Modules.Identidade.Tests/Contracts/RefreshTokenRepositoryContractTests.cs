using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.RefreshTokens;

namespace SistemaX.Modules.Identidade.Tests.Contracts;

/// <summary>Contract test do port <see cref="IRefreshTokenRepository"/> — roda 2× (InMemory + SQLite).</summary>
public abstract class RefreshTokenRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string UsuarioA = "user-a";

    protected abstract IRefreshTokenRepository CriarRepositorio();

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Salvar_e_obter_por_hash_retorna_o_mesmo_registro()
    {
        var repo = CriarRepositorio();
        var registro = RefreshTokenRegistro.Emitir(BusinessA, UsuarioA, "hash-1", Agora, TimeSpan.FromDays(30));

        await repo.SalvarAsync(registro);
        var lido = await repo.ObterPorHashAsync("hash-1");

        Assert.NotNull(lido);
        Assert.Equal(registro.Id, lido!.Id);
        Assert.True(lido.Ativo);
    }

    [Fact]
    public async Task ObterPorHashAsync_hash_desconhecido_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorHashAsync("hash-inexistente"));
    }

    [Fact]
    public async Task Revogar_persiste_o_estado_revogado()
    {
        var repo = CriarRepositorio();
        var registro = RefreshTokenRegistro.Emitir(BusinessA, UsuarioA, "hash-2", Agora, TimeSpan.FromDays(30));
        await repo.SalvarAsync(registro);

        registro.Revogar(Agora.AddMinutes(1));
        await repo.SalvarAsync(registro);

        var lido = await repo.ObterPorHashAsync("hash-2");
        Assert.False(lido!.Ativo);
        Assert.NotNull(lido.RevogadoEm);
    }

    [Fact]
    public async Task RevogarPorRotacao_persiste_o_sucessor()
    {
        var repo = CriarRepositorio();
        var registro = RefreshTokenRegistro.Emitir(BusinessA, UsuarioA, "hash-3", Agora, TimeSpan.FromDays(30));
        await repo.SalvarAsync(registro);

        registro.RevogarPorRotacao("sucessor-id", Agora.AddMinutes(1));
        await repo.SalvarAsync(registro);

        var lido = await repo.ObterPorHashAsync("hash-3");
        Assert.False(lido!.Ativo);
        Assert.Equal("sucessor-id", lido.SubstituidoPorId);
    }

    [Fact]
    public async Task RevogarTodosDoUsuarioAsync_revoga_so_ativos_do_usuario_e_tenant()
    {
        var repo = CriarRepositorio();
        var ativo1 = RefreshTokenRegistro.Emitir(BusinessA, UsuarioA, "hash-4", Agora, TimeSpan.FromDays(30));
        var ativo2 = RefreshTokenRegistro.Emitir(BusinessA, UsuarioA, "hash-5", Agora, TimeSpan.FromDays(30));
        var jaRevogado = RefreshTokenRegistro.Emitir(BusinessA, UsuarioA, "hash-6", Agora, TimeSpan.FromDays(30));
        jaRevogado.Revogar(Agora);
        var deOutroUsuario = RefreshTokenRegistro.Emitir(BusinessA, "outro-user", "hash-7", Agora, TimeSpan.FromDays(30));

        await repo.SalvarAsync(ativo1);
        await repo.SalvarAsync(ativo2);
        await repo.SalvarAsync(jaRevogado);
        await repo.SalvarAsync(deOutroUsuario);

        await repo.RevogarTodosDoUsuarioAsync(BusinessA, UsuarioA, Agora.AddMinutes(10));

        Assert.False((await repo.ObterPorHashAsync("hash-4"))!.Ativo);
        Assert.False((await repo.ObterPorHashAsync("hash-5"))!.Ativo);
        Assert.True((await repo.ObterPorHashAsync("hash-7"))!.Ativo);
    }
}
