using System.Text;
using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class TableExtractor : BaseExtractor
{
    public TableExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Table;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();
        var filter = FormatSchemaFilterSimple(schemas);

        var tablesSql = $@"
            SELECT schemaname, tablename
            FROM pg_tables
            WHERE {filter}
            ORDER BY schemaname, tablename";

        var tables = new List<(string schema, string name)>();

        using (var conn = OpenConnection())
        using (var cmd = new NpgsqlCommand(tablesSql, conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        foreach (var (schema, tableName) in tables)
        {
            ct.ThrowIfCancellationRequested();
            var ddl = await BuildTableDdlAsync(schema, tableName, ct);
            results.Add(new DatabaseObject
            {
                Name = tableName,
                Schema = schema,
                ObjectType = DatabaseObjectType.Table,
                DdlScript = ddl
            });
        }

        return results;
    }

    private async Task<string> BuildTableDdlAsync(string schema, string table, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {QuoteId(schema)}.{QuoteId(table)} (");

        var columns = await GetColumnsAsync(schema, table, ct);
        for (var i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            sb.Append($"    {QuoteId(col.name)} {col.dataType}");

            if (col.defaultValue != null)
                sb.Append($" DEFAULT {col.defaultValue}");
            if (!col.isNullable)
                sb.Append(" NOT NULL");

            if (i < columns.Count - 1)
                sb.AppendLine(",");
        }

        var constraints = await GetConstraintsAsync(schema, table, ct);
        if (constraints.Count > 0)
        {
            sb.AppendLine(",");
            for (var i = 0; i < constraints.Count; i++)
            {
                sb.Append($"    CONSTRAINT {QuoteId(constraints[i].name)} {constraints[i].definition}");
                if (i < constraints.Count - 1)
                    sb.AppendLine(",");
            }
        }

        sb.AppendLine();
        sb.Append(");");

        return sb.ToString();
    }

    private async Task<List<ColumnInfo>> GetColumnsAsync(string schema, string table, CancellationToken ct)
    {
        var sql = @"
            SELECT a.attname,
                   pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                   NOT a.attnotnull AS is_nullable,
                   pg_get_expr(d.adbin, d.adrelid) AS default_value
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
            WHERE n.nspname = @schema
              AND c.relname = @table
              AND a.attnum > 0
              AND NOT a.attisdropped
            ORDER BY a.attnum";

        var columns = new List<ColumnInfo>();

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            ));
        }

        return columns;
    }

    private async Task<List<ConstraintInfo>> GetConstraintsAsync(string schema, string table, CancellationToken ct)
    {
        var sql = @"
            SELECT conname, pg_get_constraintdef(c.oid, true)
            FROM pg_constraint c
            JOIN pg_namespace n ON n.oid = c.connamespace
            WHERE conrelid = (
                SELECT oid FROM pg_class
                WHERE relname = @table
                  AND relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = @schema)
            )
            ORDER BY
                CASE contype
                    WHEN 'p' THEN 0
                    WHEN 'u' THEN 1
                    WHEN 'f' THEN 2
                    WHEN 'c' THEN 3
                    ELSE 4
                END,
                conname";

        var constraints = new List<ConstraintInfo>();

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            constraints.Add(new ConstraintInfo(reader.GetString(0), reader.GetString(1)));
        }

        return constraints;
    }

    private static string QuoteId(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    private sealed record ColumnInfo(string name, string dataType, bool isNullable, string? defaultValue);
    private sealed record ConstraintInfo(string name, string definition);
}
