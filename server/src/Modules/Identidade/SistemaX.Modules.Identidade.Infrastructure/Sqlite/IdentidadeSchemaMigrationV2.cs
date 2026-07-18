using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>
/// Migração v2 do módulo "identidade" — tabela <c>refresh_tokens</c> (§3 do escopo F2). Só o HASH
/// do token (<c>token_hash</c>) é persistido, nunca o bruto — ver <c>RefreshTokenRegistro</c>.
/// Índice único em <c>token_hash</c> (lookup do refresh é sempre por hash exato, nunca por prefixo/
/// LIKE) + índice em <c>(usuario_id, revogado_em)</c> para a revogação em massa (reset de senha,
/// detecção de reuso).
/// </summary>
public sealed class IdentidadeSchemaMigrationV2 : SqlModuleSchemaMigration
{
    public override string Modulo => "identidade";

    public override int Versao => 2;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS refresh_tokens (
            id                  TEXT PRIMARY KEY,
            business_id         TEXT NOT NULL,
            usuario_id          TEXT NOT NULL,
            token_hash          TEXT NOT NULL,
            criado_em           TEXT NOT NULL,
            expira_em           TEXT NOT NULL,
            revogado_em         TEXT NULL,
            substituido_por_id  TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_refresh_tokens_hash ON refresh_tokens (token_hash);
        CREATE INDEX IF NOT EXISTS ix_refresh_tokens_usuario_ativo ON refresh_tokens (usuario_id, revogado_em);
        """;
}
