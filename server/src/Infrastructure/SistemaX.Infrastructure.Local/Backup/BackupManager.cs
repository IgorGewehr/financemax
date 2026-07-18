using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaX.Infrastructure.Local.Backup;

/// <inheritdoc cref="IBackupManager"/>
public sealed class BackupManager(
    ILocalSqliteConnectionFactory connectionFactory,
    IOptions<LocalDatabaseOptions> options,
    ILogger<BackupManager> logger) : IBackupManager
{
    private const string BackupPrefix = "db-";
    private const string BackupExtension = ".sqlite";

    public async Task<BackupResult> CreateBackupAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        Directory.CreateDirectory(opts.BackupDirectory);

        var freeSpace = GetFreeDiskSpaceBytes(opts.BackupDirectory);
        if (freeSpace < opts.MinFreeDiskSpaceBytesForBackup)
        {
            // Fraqueza corrigida do Supermarket-OS: lá, disco cheio fazia o copyFileSync falhar
            // silenciosamente dentro de um try/catch genérico, e o operador nunca sabia que
            // estava sem backup válido. Aqui recusamos explicitamente e logamos CRÍTICO — quem
            // compõe o host pode assinar este log e alertar o admin.
            logger.LogCritical(
                "Backup recusado: espaço livre em disco ({FreeSpaceMb}MB) abaixo do mínimo configurado ({MinMb}MB). Backup NÃO foi criado.",
                freeSpace / (1024 * 1024), opts.MinFreeDiskSpaceBytesForBackup / (1024 * 1024));
            return new BackupResult(false, null, "EspacoEmDiscoInsuficiente");
        }

        try
        {
            await using (var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false))
            await using (var cmd = connection.CreateCommand())
            {
                // Garante que todas as mudanças do -wal foram levadas ao arquivo principal
                // ANTES de copiar — senão o backup poderia faltar transações recentes.
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            var destination = Path.Combine(opts.BackupDirectory, $"{BackupPrefix}{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{BackupExtension}");

            // Cópia via stream assíncrono — nunca File.Copy síncrono, que bloquearia a thread
            // que atende UI/IPC/HTTP em bases grandes (fraqueza corrigida do Supermarket-OS).
            await using (var source = new FileStream(connectionFactory.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 81920, useAsync: true))
            await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
            }

            logger.LogInformation("Backup criado com sucesso em {Destino}.", destination);

            await CleanupOldBackupsAsync(ct).ConfigureAwait(false);
            return new BackupResult(true, destination, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Falha de I/O ao criar backup do banco local.");
            return new BackupResult(false, null, ex.Message);
        }
    }

    public Task CleanupOldBackupsAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!Directory.Exists(opts.BackupDirectory))
        {
            return Task.CompletedTask;
        }

        var backups = EnumerateBackupsNewestFirst(opts.BackupDirectory).ToList();
        foreach (var stale in backups.Skip(opts.MaxBackups))
        {
            try
            {
                File.Delete(stale.FullName);
                logger.LogDebug("Backup antigo removido: {Arquivo}.", stale.Name);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Não foi possível remover backup antigo {Arquivo} — tentará de novo no próximo ciclo.", stale.Name);
            }
        }

        return Task.CompletedTask;
    }

    public string? FindMostRecentBackup()
    {
        var opts = options.Value;
        if (!Directory.Exists(opts.BackupDirectory))
        {
            return null;
        }

        return EnumerateBackupsNewestFirst(opts.BackupDirectory).FirstOrDefault()?.FullName;
    }

    private static IEnumerable<FileInfo> EnumerateBackupsNewestFirst(string backupDirectory)
        => new DirectoryInfo(backupDirectory)
            .GetFiles($"{BackupPrefix}*{BackupExtension}")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal); // nome tem timestamp ordenável lexicograficamente

    private static long GetFreeDiskSpaceBytes(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(root))
        {
            return long.MaxValue; // não foi possível determinar — não bloquear o backup por isso
        }

        try
        {
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return long.MaxValue;
        }
    }
}
