using SistemaX.Modules.Identidade.Application.Auth;

namespace SistemaX.Modules.Identidade.Tests.Auth;

public sealed class PoliticaDeSenhaTests
{
    [Fact]
    public void Aceita_senha_forte_o_suficiente()
    {
        var resultado = PoliticaDeSenha.Validar("Tr0uxa!Segura9x", "Fulano", "fulano@exemplo.com");
        Assert.True(resultado.Sucesso);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Recusa_senha_vazia(string? senha)
    {
        Assert.True(PoliticaDeSenha.Validar(senha).Falha);
    }

    [Fact]
    public void Recusa_senha_curta_demais()
    {
        Assert.True(PoliticaDeSenha.Validar("Ab1!xyz").Falha); // 7 caracteres
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("password")]
    [InlineData("qwertyui")]
    public void Recusa_senhas_triviais_conhecidas(string senha)
    {
        Assert.True(PoliticaDeSenha.Validar(senha).Falha);
    }

    [Fact]
    public void Recusa_todos_os_caracteres_iguais()
    {
        Assert.True(PoliticaDeSenha.Validar("aaaaaaaa").Falha);
    }

    [Fact]
    public void Recusa_sequencia_monotonica_crescente()
    {
        Assert.True(PoliticaDeSenha.Validar("abcdefgh").Falha);
    }

    [Fact]
    public void Recusa_sequencia_monotonica_decrescente()
    {
        Assert.True(PoliticaDeSenha.Validar("87654321").Falha);
    }

    [Fact]
    public void Recusa_senha_que_contem_o_email_do_usuario()
    {
        var resultado = PoliticaDeSenha.Validar("fulano@exemplo.com123", "Fulano", "fulano@exemplo.com");
        Assert.True(resultado.Falha);
        Assert.Equal("identidade.senha.contem_dado_pessoal", resultado.Erro.Codigo);
    }

    [Fact]
    public void Recusa_senha_que_contem_o_nome_do_usuario()
    {
        var resultado = PoliticaDeSenha.Validar("MariaSilva2026!", "Maria Silva", "maria@exemplo.com");
        Assert.True(resultado.Falha);
    }
}
