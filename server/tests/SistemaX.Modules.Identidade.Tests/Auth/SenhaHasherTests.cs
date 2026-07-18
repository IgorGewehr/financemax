using SistemaX.Modules.Identidade.Application.Auth;

namespace SistemaX.Modules.Identidade.Tests.Auth;

public sealed class SenhaHasherTests
{
    [Fact]
    public void Hash_e_Verificar_aceita_a_senha_correta()
    {
        var hash = SenhaHasher.Hash("uma-senha-forte-123");
        Assert.True(SenhaHasher.Verificar("uma-senha-forte-123", hash));
    }

    [Fact]
    public void Verificar_recusa_senha_errada()
    {
        var hash = SenhaHasher.Hash("uma-senha-forte-123");
        Assert.False(SenhaHasher.Verificar("outra-senha", hash));
    }

    [Fact]
    public void Hash_produz_saidas_diferentes_para_a_mesma_senha_salt_aleatorio()
    {
        var hash1 = SenhaHasher.Hash("mesma-senha-123");
        var hash2 = SenhaHasher.Hash("mesma-senha-123");

        Assert.NotEqual(hash1, hash2);
        Assert.True(SenhaHasher.Verificar("mesma-senha-123", hash1));
        Assert.True(SenhaHasher.Verificar("mesma-senha-123", hash2));
    }

    [Fact]
    public void Hash_carrega_os_parametros_argon2id_no_proprio_texto()
    {
        var hash = SenhaHasher.Hash("qualquer-senha-123");
        Assert.StartsWith("argon2id$v=19$", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hash-corrompido-sem-formato")]
    [InlineData("argon2id$v=19$m=abc,t=3,p=2$c2FsdA==$aGFzaA==")]
    public void Verificar_devolve_false_para_hash_em_formato_invalido_nunca_lanca(string hashInvalido)
    {
        Assert.False(SenhaHasher.Verificar("qualquer-senha", hashInvalido));
    }
}
