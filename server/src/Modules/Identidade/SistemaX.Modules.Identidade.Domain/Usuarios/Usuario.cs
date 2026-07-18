using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Domain.Usuarios;

/// <summary>
/// Usuário do financemax — IDENTIDADE net-new desta fatia (F2). Diferente do <c>Usuario</c> do
/// SistemaX (ADR-0003, PIN local): aqui a credencial é e-mail + senha (<see cref="SenhaHash"/>,
/// Argon2id via <c>SenhaHasher</c>), pensada para acesso ONLINE multi-usuário (dono no Mac +
/// colega no Windows, mesma base — MVP-ESCOPO.md).
///
/// <see cref="BusinessId"/> existe por uniformidade com R1 (todo agregado do sistema carrega o
/// tenant) mas o financemax F2 é SINGLE-TENANT fixo (um businessId por instalação, MVP-ESCOPO.md
/// "fora de escopo agora: multi-tenant") — nunca resolvido do request, sempre da config do host
/// (mesmo racional do <c>StubAuthMiddleware</c> que este agregado substitui).
///
/// E-mail é ÚNICO por negócio (verificado pelo caso de uso antes de <see cref="Criar"/>, nunca
/// aqui — o agregado não tem acesso ao repositório).
/// </summary>
public sealed class Usuario : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string Nome { get; private set; }
    public string Email { get; }
    public string SenhaHash { get; private set; }
    public Papel Papel { get; private set; }
    public bool Ativo { get; private set; }

    /// <summary>Força troca de senha no próximo login — usado pelo seed de bootstrap (senha
    /// inicial conhecida) e por reset administrativo (§4 do prompt de escopo).</summary>
    public bool MustChangePassword { get; private set; }

    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    private Usuario(
        string id, string businessId, string nome, string email, string senhaHash, Papel papel,
        bool ativo, bool mustChangePassword, DateTimeOffset criadoEm, DateTimeOffset atualizadoEm)
    {
        Id = id;
        BusinessId = businessId;
        Nome = nome;
        Email = email;
        SenhaHash = senhaHash;
        Papel = papel;
        Ativo = ativo;
        MustChangePassword = mustChangePassword;
        CriadoEm = criadoEm;
        AtualizadoEm = atualizadoEm;
    }

    public static Result<Usuario> Criar(
        string businessId, string nome, string email, string senhaHash, Papel papel, DateTimeOffset agora,
        bool ativo = true, bool mustChangePassword = false)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<Usuario>(new Error("identidade.usuario.business_obrigatorio", "BusinessId é obrigatório."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<Usuario>(new Error("identidade.usuario.nome_obrigatorio", "Nome é obrigatório."));

        var emailNormalizado = NormalizarEmail(email);
        if (emailNormalizado is null)
            return Result.Falhar<Usuario>(new Error("identidade.usuario.email_invalido", "E-mail inválido."));

        if (string.IsNullOrWhiteSpace(senhaHash))
            return Result.Falhar<Usuario>(new Error("identidade.usuario.senha_obrigatoria", "Hash de senha é obrigatório."));

        return Result.Ok(new Usuario(
            IdGenerator.NovoId(), businessId, nome.Trim(), emailNormalizado, senhaHash, papel,
            ativo, mustChangePassword, agora, agora));
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static Usuario Reconstituir(
        string id, string businessId, string nome, string email, string senhaHash, Papel papel,
        bool ativo, bool mustChangePassword, DateTimeOffset criadoEm, DateTimeOffset atualizadoEm)
        => new(id, businessId, nome, email, senhaHash, papel, ativo, mustChangePassword, criadoEm, atualizadoEm);

    /// <summary>E-mail normalizado (trim + minúsculas) — chave de unicidade por negócio. Devolve
    /// <c>null</c> se vazio/sem formato mínimo de e-mail (presença de "@" com algo antes/depois),
    /// checagem propositalmente barata (validação forte de RFC 5322 é trabalho de UI, não de
    /// invariante de domínio).</summary>
    public static string? NormalizarEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalizado = email.Trim().ToLowerInvariant();
        var arroba = normalizado.IndexOf('@');
        if (arroba <= 0 || arroba == normalizado.Length - 1 || normalizado.Contains(' ')) return null;
        return normalizado;
    }

    public void AlterarSenha(string novoSenhaHash, DateTimeOffset agora, bool mustChangePassword = false)
    {
        SenhaHash = novoSenhaHash;
        MustChangePassword = mustChangePassword;
        AtualizadoEm = agora;
    }

    public void AlterarPapel(Papel novoPapel, DateTimeOffset agora)
    {
        Papel = novoPapel;
        AtualizadoEm = agora;
    }

    public void Ativar(DateTimeOffset agora)
    {
        Ativo = true;
        AtualizadoEm = agora;
    }

    public void Desativar(DateTimeOffset agora)
    {
        Ativo = false;
        AtualizadoEm = agora;
    }

    public void RenomearPara(string novoNome, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(novoNome)) return;
        Nome = novoNome.Trim();
        AtualizadoEm = agora;
    }
}

file static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
