using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Domain.Convites;

public enum StatusConvite
{
    Pendente,
    Aceito,
    Expirado,
    Revogado,
}

/// <summary>
/// Convite de um usuário existente (founder/admin) para outro se cadastrar com um papel
/// pré-definido — a segunda porta de entrada do onboarding (a primeira é o first-run de
/// <c>RegistrarUseCase</c>, reservado ao 1º dono; demais só entram com convite válido). Mesmo molde
/// de segredo do <see cref="Domain.RefreshTokens.RefreshTokenRegistro"/>: só o HASH do token vive
/// aqui/no banco (<see cref="TokenHash"/>) — o bruto (o que vai no link repassado ao convidado) só
/// existe uma vez, na resposta de <c>POST /api/convites</c>, nunca persistido.
///
/// Ciclo de vida por TIMESTAMPS explícitos (não um campo <c>status</c> solto — mesma razão do
/// <c>RefreshTokenRegistro</c>: dois carimbos nulos por padrão contam a história inteira sem risco
/// de ficarem inconsistentes entre si): <see cref="AceitoEm"/> e <see cref="RevogadoEm"/> são
/// mutuamente exclusivos (o domínio nunca permite os dois setados) e "Expirado" nunca é GRAVADO —
/// é sempre <see cref="ExpiraEm"/> comparado contra o "agora" de quem pergunta
/// (<see cref="Status"/>), porque o relógio de parede não espera ninguém rodar um job pra marcar
/// expiração.
/// </summary>
public sealed class Convite : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string Email { get; }
    public Papel Papel { get; }
    public string TokenHash { get; }
    public string CriadoPorUsuarioId { get; }
    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset ExpiraEm { get; }
    public DateTimeOffset? AceitoEm { get; private set; }
    public DateTimeOffset? RevogadoEm { get; private set; }

    private Convite(
        string id, string businessId, string email, Papel papel, string tokenHash, string criadoPorUsuarioId,
        DateTimeOffset criadoEm, DateTimeOffset expiraEm, DateTimeOffset? aceitoEm, DateTimeOffset? revogadoEm)
    {
        Id = id;
        BusinessId = businessId;
        Email = email;
        Papel = papel;
        TokenHash = tokenHash;
        CriadoPorUsuarioId = criadoPorUsuarioId;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
        AceitoEm = aceitoEm;
        RevogadoEm = revogadoEm;
    }

    public static Result<Convite> Criar(
        string businessId, string email, Papel papel, string tokenHash, string criadoPorUsuarioId,
        DateTimeOffset agora, TimeSpan validade)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<Convite>(new Error("identidade.convite.business_obrigatorio", "BusinessId é obrigatório."));

        var emailNormalizado = Usuario.NormalizarEmail(email);
        if (emailNormalizado is null)
            return Result.Falhar<Convite>(new Error("identidade.convite.email_invalido", "E-mail inválido."));

        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Falhar<Convite>(new Error("identidade.convite.token_obrigatorio", "Hash de token é obrigatório."));

        if (string.IsNullOrWhiteSpace(criadoPorUsuarioId))
            return Result.Falhar<Convite>(new Error("identidade.convite.criado_por_obrigatorio", "Convite precisa de um autor."));

        if (validade <= TimeSpan.Zero)
            return Result.Falhar<Convite>(new Error("identidade.convite.validade_invalida", "Validade do convite deve ser positiva."));

        return Result.Ok(new Convite(
            IdGenerator.NovoId(), businessId, emailNormalizado, papel, tokenHash, criadoPorUsuarioId,
            agora, agora + validade, null, null));
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static Convite Reconstituir(
        string id, string businessId, string email, Papel papel, string tokenHash, string criadoPorUsuarioId,
        DateTimeOffset criadoEm, DateTimeOffset expiraEm, DateTimeOffset? aceitoEm, DateTimeOffset? revogadoEm)
        => new(id, businessId, email, papel, tokenHash, criadoPorUsuarioId, criadoEm, expiraEm, aceitoEm, revogadoEm);

    /// <summary>Status efetivo NO INSTANTE <paramref name="agora"/> — nunca cacheado, sempre
    /// recalculado (ver comentário da classe).</summary>
    public StatusConvite Status(DateTimeOffset agora)
    {
        if (AceitoEm is not null) return StatusConvite.Aceito;
        if (RevogadoEm is not null) return StatusConvite.Revogado;
        return agora >= ExpiraEm ? StatusConvite.Expirado : StatusConvite.Pendente;
    }

    /// <summary>Marca como aceito — falha se já aceito, revogado ou expirado (invariante central:
    /// um convite só pode ser usado UMA vez, dentro da janela de validade, nunca depois de
    /// revogado). Quem chama (<c>RegistrarUseCase</c>) já filtrou por e-mail antes de chegar aqui;
    /// este método é a ÚLTIMA linha de defesa contra corrida (duas tentativas de registro
    /// concorrentes com o mesmo token).</summary>
    public Result Aceitar(DateTimeOffset agora)
    {
        if (AceitoEm is not null)
            return Result.Falhar(new Error("identidade.convite.ja_aceito", "Convite já foi aceito."));

        if (RevogadoEm is not null)
            return Result.Falhar(new Error("identidade.convite.revogado", "Convite foi revogado."));

        if (agora >= ExpiraEm)
            return Result.Falhar(new Error("identidade.convite.expirado", "Convite expirado."));

        AceitoEm = agora;
        return Result.Ok();
    }

    /// <summary>Revoga — IDEMPOTENTE se já revogado (mesmo racional de
    /// <c>RefreshTokenRegistro.Revogar</c>), mas falha se já ACEITO: revogar um convite que já virou
    /// uma conta de verdade não desfaz a conta — quem quiser tirar o acesso usa
    /// <c>PATCH /usuarios/{id}</c> (desativar o usuário), não isto.</summary>
    public Result Revogar(DateTimeOffset agora)
    {
        if (AceitoEm is not null)
            return Result.Falhar(new Error("identidade.convite.ja_aceito", "Convite já foi aceito — revogue o usuário, não o convite."));

        if (RevogadoEm is not null)
            return Result.Ok();

        RevogadoEm = agora;
        return Result.Ok();
    }
}

file static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
