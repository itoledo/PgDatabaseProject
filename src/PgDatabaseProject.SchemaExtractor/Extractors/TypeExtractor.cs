using System.Text;
using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class TypeExtractor : BaseExtractor
{
    public TypeExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Type;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();

        await ExtractEnumTypesAsync(schemas, results, ct);
        await ExtractCompositeTypesAsync(schemas, results, ct);
        await ExtractDomainTypesAsync(schemas, results, ct);

        results.Sort((a, b) => string.Compare($"{a.Schema}.{a.Name}", $"{b.Schema}.{b.Name}", StringComparison.Ordinal));
        return results;
    }

    private async Task ExtractEnumTypesAsync(IReadOnlyList<string> schemas, List<DatabaseObject> results, CancellationToken ct)
    {
        var filter = FormatSchemaFilter(schemas);

        var sql = $@"
            SELECT n.nspname, t.typname,
                   array_agg(e.enumlabel ORDER BY e.enumsortorder) AS labels
            FROM pg_type t
            JOIN pg_namespace n ON t.typnamespace = n.oid
            JOIN pg_enum e ON e.enumtypid = t.oid
            WHERE {filter}
            GROUP BY n.nspname, t.typname
            ORDER BY n.nspname, t.typname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var labels = (string[])reader.GetValue(2);

            var quotedLabels = string.Join(",\n    ", labels.Select(l => $"'{EscapeSql(l)}'"));
            var ddl = $"CREATE TYPE {QuoteId(schema)}.{QuoteId(name)} AS ENUM (\n    {quotedLabels}\n);";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Type,
                DdlScript = ddl
            });
        }
    }

    private async Task ExtractCompositeTypesAsync(IReadOnlyList<string> schemas, List<DatabaseObject> results, CancellationToken ct)
    {
        var filter = FormatSchemaFilter(schemas);

        var sql = $@"
            SELECT n.nspname, t.typname, t.oid
            FROM pg_type t
            JOIN pg_namespace n ON t.typnamespace = n.oid
            WHERE t.typtype = 'c'
              AND {filter}
              AND NOT EXISTS (
                  SELECT 1 FROM pg_class c
                  WHERE c.reltype = t.oid AND c.relkind IN ('r', 'v', 'm', 'p')
              )
            ORDER BY n.nspname, t.typname";

        var types = new List<(string schema, string name, long oid)>();

        using (var conn = OpenConnection())
        using (var cmd = new NpgsqlCommand(sql, conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var oid = Convert.ToInt64(reader.GetValue(2));
                types.Add((reader.GetString(0), reader.GetString(1), oid));
            }
        }

        foreach (var (schema, name, oid) in types)
        {
            var attrSql = @"
                SELECT a.attname, pg_catalog.format_type(a.atttypid, a.atttypmod)
                FROM pg_attribute a
                WHERE a.attrelid = (SELECT typrelid FROM pg_type WHERE oid = @oid)
                  AND a.attnum > 0
                  AND NOT a.attisdropped
                ORDER BY a.attnum";

            using var conn2 = OpenConnection();
            using var attrCmd = new NpgsqlCommand(attrSql, conn2);
            attrCmd.Parameters.AddWithValue("oid", oid);
            using var attrReader = await attrCmd.ExecuteReaderAsync(ct);

            var sb = new StringBuilder();
            sb.Append($"CREATE TYPE {QuoteId(schema)}.{QuoteId(name)} AS (\n");
            var first = true;
            while (await attrReader.ReadAsync(ct))
            {
                if (!first) sb.Append(",\n");
                sb.Append($"    {QuoteId(attrReader.GetString(0))} {attrReader.GetString(1)}");
                first = false;
            }
            sb.Append("\n);");

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Type,
                DdlScript = sb.ToString()
            });
        }
    }

    private async Task ExtractDomainTypesAsync(IReadOnlyList<string> schemas, List<DatabaseObject> results, CancellationToken ct)
    {
        var filter = FormatSchemaFilter(schemas);

        var sql = $@"
            SELECT n.nspname, t.typname,
                   pg_catalog.format_type(t.typbasetype, t.typtypmod) AS base_type,
                   t.typnotnull, t.typdefault,
                   (SELECT string_agg(pg_get_constraintdef(c.oid, true), ' ')
                    FROM pg_constraint c WHERE c.contypid = t.oid) AS constraints
            FROM pg_type t
            JOIN pg_namespace n ON t.typnamespace = n.oid
            WHERE t.typtype = 'd'
              AND {filter}
            ORDER BY n.nspname, t.typname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var baseType = reader.GetString(2);
            var notNull = reader.GetBoolean(3);
            var defaultVal = reader.IsDBNull(4) ? null : reader.GetString(4);
            var constraints = reader.IsDBNull(5) ? null : reader.GetString(5);

            var sb = new StringBuilder();
            sb.Append($"CREATE DOMAIN {QuoteId(schema)}.{QuoteId(name)} AS {baseType}");
            if (defaultVal != null)
                sb.Append($"\n    DEFAULT {defaultVal}");
            if (notNull)
                sb.Append("\n    NOT NULL");
            if (constraints != null)
                sb.Append($"\n    {constraints}");
            sb.Append(';');

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Type,
                DdlScript = sb.ToString()
            });
        }
    }

    private static string QuoteId(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
