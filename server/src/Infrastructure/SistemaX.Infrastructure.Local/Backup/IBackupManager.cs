namespace SistemaX.Infrastructure.Local.Backup;

public sealed record BackupResult(bool Sucesso, string? CaminhoArquivo, string? MotivoFalha);

/// <summary>
/// Backup = checkpoint do WAL + cópia de arquivo, nunca dump lógico — muito mais barato e
/// rápido que exportar dados linha a linha (ver docs/robustez §2). O gatilho é um EVENTO DE
/// NEGÓCIO relevante (ex.: abertura de caixa), não só um timer — quem decide QUANDO chamar
/// <see cref="CreateBackupAsync"/> é o host/módulo de negócio; esta classe só sabe COMO.
/// </summary>
public interface IBackupManager
{
    /// <summary>
    /// Faz <c>PRAGMA wal_checkpoint(TRUNCATE)</c> (garante que o WAL foi todo levado ao arquivo
    /// principal) e copia para <c>backups/db-{timestampUtc}.sqlite</c> de forma assíncrona (I/O
    /// em stream, nunca <c>File.Copy</c> síncrono bloqueando a thread de UI/IPC — fraqueza
    /// corrigida do Supermarket-OS). Recusa o backup (sem lançar) se o espaço livre em disco
    /// estiver abaixo de <see cref="LocalDatabaseOptions.MinFreeDiskSpaceBytesForBackup"/>.
    /// </summary>
    Task<BackupResult> CreateBackupAsync(CancellationToken ct = default);

    /// <summary>Mantém só os <see cref="LocalDatabaseOptions.MaxBackups"/> mais recentes.</summary>
    Task CleanupOldBackupsAsync(CancellationToken ct = default);

    /// <summary>Caminho do backup mais recente, ou <c>null</c> se nenhum existir.</summary>
    string? FindMostRecentBackup();
}
