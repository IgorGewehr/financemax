using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>
/// Migração v3 do módulo "identidade" — tabela <c>convites</c> (onboarding: convite por e-mail).
/// Só o hash do token é gravado (<c>token_hash</c>, índice único — lookup sempre por hash exato,
/// nunca por prefixo), mesmo racional de <c>refresh_tokens</c> em
/// <see cref="IdentidadeSchemaMigrationV2"/>. <c>aceito_em</c>/<c>revogado_em</c> nulos por padrão —
/// "expirado" nunca é uma coluna, é sempre <c>expira_em</c> comparado contra o relógio de quem
/// pergunta (ver <c>Convite.Status</c>).
/// </summary>
public sealed class IdentidadeSchemaMigrationV3 : SqlModuleSchemaMigration
{
    public override string Modulo => "identidade";

    public override int Versao => 3;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS convites (
            id                     TEXT PRIMARY KEY,
            business_id            TEXT NOT NULL,
            email                  TEXT NOT NULL,
            papel                  TEXT NOT NULL,
            token_hash             TEXT NOT NULL,
            criado_por_usuario_id  TEXT NOT NULL,
            criado_em              TEXT NOT NULL,
            expira_em              TEXT NOT NULL,
            aceito_em              TEXT NULL,
            revogado_em            TEXT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_convites_token_hash ON convites (token_hash);
        CREATE INDEX IF NOT EXISTS ix_convites_business_pendentes ON convites (business_id, aceito_em, revogado_em);
        """;
}
