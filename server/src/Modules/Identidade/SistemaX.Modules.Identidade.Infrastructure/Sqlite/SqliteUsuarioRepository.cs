using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>Persistência REAL (SQLite) de <see cref="Usuario"/> — mesmo molde de
/// <c>SqliteAporteDeCapitalRepository</c> (Financeiro). Schema nasce de <see cref="IdentidadeSchemaMigrationV1"/>.</summary>
public sealed class SqliteUsuarioRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao) : IUsuarioRepository
{
    private const string Colunas = "id, business_id, nome, email, senha_hash, papel, ativo, must_change_password, criado_em, atualizado_em";

    public Task<Usuario?> ObterPorIdAsync(string businessId, string usuarioId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM usuarios WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", usuarioId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<Usuario?> ObterPorEmailAsync(string businessId, string email, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM usuarios WHERE business_id = $biz AND email = $email;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$email", email);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<Usuario>> ListarAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM usuarios WHERE business_id = $biz ORDER BY nome;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<Usuario>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<Usuario>)resultado;
        }, ct);

    public Task SalvarAsync(Usuario usuario, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO usuarios (id, business_id, nome, email, senha_hash, papel, ativo, must_change_password, criado_em, atualizado_em)
                VALUES ($id, $biz, $nome, $email, $senhaHash, $papel, $ativo, $mustChange, $criadoEm, $atualizadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    nome                  = excluded.nome,
                    senha_hash            = excluded.senha_hash,
                    papel                 = excluded.papel,
                    ativo                 = excluded.ativo,
                    must_change_password  = excluded.must_change_password,
                    atualizado_em         = excluded.atualizado_em;
                """;
            cmd.Parameters.AddWithValue("$id", usuario.Id);
            cmd.Parameters.AddWithValue("$biz", usuario.BusinessId);
            cmd.Parameters.AddWithValue("$nome", usuario.Nome);
            cmd.Parameters.AddWithValue("$email", usuario.Email);
            cmd.Parameters.AddWithValue("$senhaHash", usuario.SenhaHash);
            cmd.Parameters.AddWithValue("$papel", usuario.Papel.ToString());
            cmd.Parameters.AddWithValue("$ativo", usuario.Ativo ? 1 : 0);
            cmd.Parameters.AddWithValue("$mustChange", usuario.MustChangePassword ? 1 : 0);
            cmd.Parameters.AddWithValue("$criadoEm", IsoInstante(usuario.CriadoEm));
            cmd.Parameters.AddWithValue("$atualizadoEm", IsoInstante(usuario.AtualizadoEm));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<int> ContarFoundersAtivosAsync(string businessId, string? excetoId = null, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT COUNT(*) FROM usuarios
                WHERE business_id = $biz AND papel = $papel AND ativo = 1
                  AND ($excetoId IS NULL OR id <> $excetoId);
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$papel", Papel.Founder.ToString());
            cmd.Parameters.AddWithValue("$excetoId", (object?)excetoId ?? DBNull.Value);

            var resultado = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return Convert.ToInt32(resultado, CultureInfo.InvariantCulture);
        }, ct);

    private static Usuario Ler(SqliteDataReader reader)
        => Usuario.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            nome: reader.GetString(2),
            email: reader.GetString(3),
            senhaHash: reader.GetString(4),
            papel: Enum.Parse<Papel>(reader.GetString(5)),
            ativo: reader.GetInt64(6) != 0,
            mustChangePassword: reader.GetInt64(7) != 0,
            criadoEm: ParseInstante(reader.GetString(8)),
            atualizadoEm: ParseInstante(reader.GetString(9)));

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
