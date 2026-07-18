using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Domain.RefreshTokens;

/// <summary>
/// Registro de UM refresh token emitido (§3 do prompt de escopo: "refresh token rotativo, opaco,
/// hash guardado numa tabela, revogável, expira ~30d"). O token BRUTO (o segredo que o cliente
/// guarda) NUNCA é persistido — só <see cref="TokenHash"/> (SHA-256 do bruto, comparação
/// determinística sem round-trip de Argon2 a cada refresh: aqui o objetivo é só opacidade/
/// não-adivinhação, não resistência a rainbow table de senha humana, então hash rápido é a escolha
/// certa e não um Argon2 desnecessário).
///
/// ROTAÇÃO: um refresh emite um NOVO registro e marca este como <see cref="RevogadoEm"/> +
/// <see cref="SubstituidoPorId"/> — a cadeia permite DETECÇÃO DE REUSO (§ANTI-replay: se um token
/// já revogado for apresentado de novo, é sinal de token roubado reaparecendo depois da rotação
/// legítima — o caso de uso reage revogando TODA a cadeia do usuário).
/// </summary>
public sealed class RefreshTokenRegistro : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string UsuarioId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset ExpiraEm { get; }
    public DateTimeOffset? RevogadoEm { get; private set; }
    public string? SubstituidoPorId { get; private set; }

    public bool Ativo => RevogadoEm is null;
    public bool Expirado(DateTimeOffset agora) => agora >= ExpiraEm;

    private RefreshTokenRegistro(
        string id, string businessId, string usuarioId, string tokenHash,
        DateTimeOffset criadoEm, DateTimeOffset expiraEm, DateTimeOffset? revogadoEm, string? substituidoPorId)
    {
        Id = id;
        BusinessId = businessId;
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
        RevogadoEm = revogadoEm;
        SubstituidoPorId = substituidoPorId;
    }

    public static RefreshTokenRegistro Emitir(string businessId, string usuarioId, string tokenHash, DateTimeOffset agora, TimeSpan validade)
        => new(IdGenerator.NovoId(), businessId, usuarioId, tokenHash, agora, agora + validade, null, null);

    public static RefreshTokenRegistro Reconstituir(
        string id, string businessId, string usuarioId, string tokenHash,
        DateTimeOffset criadoEm, DateTimeOffset expiraEm, DateTimeOffset? revogadoEm, string? substituidoPorId)
        => new(id, businessId, usuarioId, tokenHash, criadoEm, expiraEm, revogadoEm, substituidoPorId);

    /// <summary>Revoga por rotação — aponta para o registro sucessor (cadeia de reuso).</summary>
    public void RevogarPorRotacao(string substituidoPorId, DateTimeOffset agora)
    {
        if (RevogadoEm is not null) return;
        RevogadoEm = agora;
        SubstituidoPorId = substituidoPorId;
    }

    /// <summary>Revoga por logout/reset administrativo/detecção de reuso — sem sucessor.</summary>
    public void Revogar(DateTimeOffset agora)
    {
        if (RevogadoEm is not null) return;
        RevogadoEm = agora;
    }
}

file static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
