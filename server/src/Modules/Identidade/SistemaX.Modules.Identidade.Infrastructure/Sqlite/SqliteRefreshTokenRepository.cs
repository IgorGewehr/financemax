using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.RefreshTokens;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>Persistência REAL (SQLite) de <see cref="RefreshTokenRegistro"/>. Schema nasce de
/// <see cref="IdentidadeSchemaMigrationV2"/>.</summary>
public sealed class SqliteRefreshTokenRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao) : IRefreshTokenRepository
{
    private const string Colunas = "id, business_id, usuario_id, token_hash, criado_em, expira_em, revogado_em, substituido_por_id";

    public Task<RefreshTokenRegistro?> ObterPorHashAsync(string tokenHash, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM refresh_tokens WHERE token_hash = $hash;";
            cmd.Parameters.AddWithValue("$hash", tokenHash);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task SalvarAsync(RefreshTokenRegistro registro, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO refresh_tokens (id, business_id, usuario_id, token_hash, criado_em, expira_em, revogado_em, substituido_por_id)
                VALUES ($id, $biz, $usuarioId, $hash, $criadoEm, $expiraEm, $revogadoEm, $substituidoPorId)
                ON CONFLICT(id) DO UPDATE SET
                    revogado_em        = excluded.revogado_em,
                    substituido_por_id = excluded.substituido_por_id;
                """;
            cmd.Parameters.AddWithValue("$id", registro.Id);
            cmd.Parameters.AddWithValue("$biz", registro.BusinessId);
            cmd.Parameters.AddWithValue("$usuarioId", registro.UsuarioId);
            cmd.Parameters.AddWithValue("$hash", registro.TokenHash);
            cmd.Parameters.AddWithValue("$criadoEm", IsoInstante(registro.CriadoEm));
            cmd.Parameters.AddWithValue("$expiraEm", IsoInstante(registro.ExpiraEm));
            cmd.Parameters.AddWithValue("$revogadoEm", (object?)(registro.RevogadoEm is { } r ? IsoInstante(r) : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$substituidoPorId", (object?)registro.SubstituidoPorId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task RevogarTodosDoUsuarioAsync(string businessId, string usuarioId, DateTimeOffset agora, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                UPDATE refresh_tokens SET revogado_em = $agora
                WHERE business_id = $biz AND usuario_id = $usuarioId AND revogado_em IS NULL;
                """;
            cmd.Parameters.AddWithValue("$agora", IsoInstante(agora));
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$usuarioId", usuarioId);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static RefreshTokenRegistro Ler(SqliteDataReader reader)
        => RefreshTokenRegistro.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            usuarioId: reader.GetString(2),
            tokenHash: reader.GetString(3),
            criadoEm: ParseInstante(reader.GetString(4)),
            expiraEm: ParseInstante(reader.GetString(5)),
            revogadoEm: reader.IsDBNull(6) ? null : ParseInstante(reader.GetString(6)),
            substituidoPorId: reader.IsDBNull(7) ? null : reader.GetString(7));

    private static string IsoInstante(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseInstante(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private async Task ExecutarAsync(Func<SqliteConnection, SqliteTransaction?, Task> acao, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            await acao(uow.Connection, uow.Transaction).ConfigureAwait(false);
            return;
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await acao(connection, null).ConfigureAwait(false);
    }

    private async Task<T> ConsultarAsync<T>(Func<SqliteConnection, SqliteTransaction?, Task<T>> consulta, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            return await consulta(uow.Connection, uow.Transaction).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await consulta(connection, null).ConfigureAwait(false);
    }
}
