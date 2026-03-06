using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class ExtensionExtractor : BaseExtractor
{
    public ExtensionExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Extension;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();

        var sql = @"
            SELECT e.extname, e.extversion, n.nspname
            FROM pg_extension e
            JOIN pg_namespace n ON n.oid = e.extnamespace
            WHERE e.extname != 'plpgsql'
            ORDER BY e.extname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var version = reader.GetString(1);
            var schema = reader.GetString(2);

            var ddl = $"-- Extension: {name} v{version}\n" +
                      $"CREATE EXTENSION IF NOT EXISTS \"{name}\"" +
                      (schema != "public" ? $"\n    SCHEMA {QuoteIdentifier(schema)}" : "") +
                      $"\n    VERSION '{EscapeSql(version)}';";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Extension,
                DdlScript = ddl
            });
        }

        return results;
    }

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
