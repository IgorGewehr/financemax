using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Convites;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>Persistência REAL (SQLite) de <see cref="Convite"/>. Schema nasce de
/// <see cref="IdentidadeSchemaMigrationV3"/>.</summary>
public sealed class SqliteConviteRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao) : IConviteRepository
{
    private const string Colunas =
        "id, business_id, email, papel, token_hash, criado_por_usuario_id, criado_em, expira_em, aceito_em, revogado_em";

    public Task<Convite?> ObterPorIdAsync(string businessId, string conviteId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM convites WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", conviteId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<Convite?> ObterPorTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM convites WHERE token_hash = $hash;";
            cmd.Parameters.AddWithValue("$hash", tokenHash);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<Convite>> ListarPendentesAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                $"""
                SELECT {Colunas} FROM convites
                WHERE business_id = $biz AND aceito_em IS NULL AND revogado_em IS NULL
                ORDER BY criado_em DESC;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<Convite>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<Convite>)resultado;
        }, ct);

    public Task SalvarAsync(Convite convite, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO convites (id, business_id, email, papel, token_hash, criado_por_usuario_id, criado_em, expira_em, aceito_em, revogado_em)
                VALUES ($id, $biz, $email, $papel, $hash, $criadoPor, $criadoEm, $expiraEm, $aceitoEm, $revogadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    aceito_em    = excluded.aceito_em,
                    revogado_em  = excluded.revogado_em;
                """;
            cmd.Parameters.AddWithValue("$id", convite.Id);
            cmd.Parameters.AddWithValue("$biz", convite.BusinessId);
            cmd.Parameters.AddWithValue("$email", convite.Email);
            cmd.Parameters.AddWithValue("$papel", convite.Papel.ToString());
            cmd.Parameters.AddWithValue("$hash", convite.TokenHash);
            cmd.Parameters.AddWithValue("$criadoPor", convite.CriadoPorUsuarioId);
            cmd.Parameters.AddWithValue("$criadoEm", IsoInstante(convite.CriadoEm));
            cmd.Parameters.AddWithValue("$expiraEm", IsoInstante(convite.ExpiraEm));
            cmd.Parameters.AddWithValue("$aceitoEm", (object?)(convite.AceitoEm is { } a ? IsoInstante(a) : null) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$revogadoEm", (object?)(convite.RevogadoEm is { } r ? IsoInstante(r) : null) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static Convite Ler(SqliteDataReader reader)
        => Convite.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            email: reader.GetString(2),
            papel: Enum.Parse<Papel>(reader.GetString(3)),
            tokenHash: reader.GetString(4),
            criadoPorUsuarioId: reader.GetString(5),
            criadoEm: ParseInstante(reader.GetString(6)),
            expiraEm: ParseInstante(reader.GetString(7)),
            aceitoEm: reader.IsDBNull(8) ? null : ParseInstante(reader.GetString(8)),
            revogadoEm: reader.IsDBNull(9) ? null : ParseInstante(reader.GetString(9)));

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
