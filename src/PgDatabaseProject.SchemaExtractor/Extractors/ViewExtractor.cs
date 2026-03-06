using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class ViewExtractor : BaseExtractor
{
    public ViewExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.View;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();
        var filter = FormatSchemaFilter(schemas);

        var sql = $@"
            SELECT n.nspname, c.relname, pg_get_viewdef(c.oid, true) AS definition
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'v'
              AND {filter}
            ORDER BY n.nspname, c.relname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var definition = reader.GetString(2);

            var ddl = $"CREATE OR REPLACE VIEW {QuoteId(schema)}.{QuoteId(name)} AS\n{definition.TrimEnd()}\n;";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.View,
                DdlScript = ddl
            });
        }

        return results;
    }

    private static string QuoteId(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
