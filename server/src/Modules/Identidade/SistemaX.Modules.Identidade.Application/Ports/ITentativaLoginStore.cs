namespace SistemaX.Modules.Identidade.Application.Ports;

/// <summary>Resultado de checar/registrar uma tentativa de login contra o LOCKOUT PROGRESSIVO
/// (§7 do escopo F2). <paramref name="Bloqueado"/> ⇒ recusar a tentativa ANTES de tocar em
/// senha/Argon2 (barato, evita até gastar CPU verificando hash de uma conta já travada).</summary>
public readonly record struct StatusBloqueio(bool Bloqueado, DateTimeOffset? BloqueadoAte);

/// <summary>
/// Contador de falhas de login por CHAVE (e-mail normalizado + businessId — deliberadamente NÃO
/// inclui IP: um atacante trocando de IP não deve resetar o contador de tentativas contra uma
/// conta-alvo específica; a proteção "por IP" é responsabilidade separada do rate limiter HTTP,
/// ver <c>Financemax.Api/Program.cs</c>). Implementação padrão é IN-MEMORY (ver
/// <c>InMemoryTentativaLoginStore</c>) — deliberado: é estado efêmero de proteção, não dado de
/// negócio; perder o contador num restart do processo é aceitável para o MVP (dono+colega,
/// servidor único) e evita mais uma tabela SQLite para algo que não precisa sobreviver a reboot.
/// </summary>
public interface ITentativaLoginStore
{
    StatusBloqueio Verificar(string chave, DateTimeOffset agora);

    /// <summary>Registra uma falha e devolve o status de bloqueio RESULTANTE (para decidir a
    /// mensagem/log sem uma segunda chamada).</summary>
    StatusBloqueio RegistrarFalha(string chave, DateTimeOffset agora);

    void RegistrarSucesso(string chave);
}
