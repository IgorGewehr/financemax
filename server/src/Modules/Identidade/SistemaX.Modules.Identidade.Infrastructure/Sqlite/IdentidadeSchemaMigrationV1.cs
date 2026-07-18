using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "identidade" — tabela <c>usuarios</c> (§2 do escopo F2). E-mail único
/// POR NEGÓCIO (índice <c>UNIQUE(business_id, email)</c>, não um <c>UNIQUE</c> global — o mesmo
/// e-mail poderia, em tese, existir em duas instalações single-tenant diferentes; hoje é moot
/// porque cada instalação só tem um <c>business_id</c>, mas a coluna documenta a regra de negócio
/// real, não um acidente de implementação).
/// </summary>
public sealed class IdentidadeSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "identidade";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS usuarios (
            id                    TEXT PRIMARY KEY,
            business_id           TEXT NOT NULL,
            nome                  TEXT NOT NULL,
            email                 TEXT NOT NULL,
            senha_hash            TEXT NOT NULL,
            papel                 TEXT NOT NULL,
            ativo                 INTEGER NOT NULL,
            must_change_password  INTEGER NOT NULL,
            criado_em             TEXT NOT NULL,
            atualizado_em         TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_usuarios_business_email ON usuarios (business_id, email);
        CREATE INDEX IF NOT EXISTS ix_usuarios_business_papel_ativo ON usuarios (business_id, papel, ativo);
        """;
}
