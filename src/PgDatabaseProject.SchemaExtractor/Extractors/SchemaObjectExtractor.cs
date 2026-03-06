using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class SchemaObjectExtractor : BaseExtractor
{
    public SchemaObjectExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Schema;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();

        var sql = @"
            SELECT nspname
            FROM pg_namespace
            WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast', 'public')
              AND nspname NOT LIKE 'pg_temp_%'
              AND nspname NOT LIKE 'pg_toast_temp_%'
            ORDER BY nspname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);

            if (schemas.Count > 0 && !schemas.Contains(name))
                continue;

            var ddl = $"CREATE SCHEMA IF NOT EXISTS {QuoteIdentifier(name)};";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = name,
                ObjectType = DatabaseObjectType.Schema,
                DdlScript = ddl
            });
        }

        return results;
    }

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
