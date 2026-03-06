using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class TriggerExtractor : BaseExtractor
{
    public TriggerExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Trigger;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();
        var filter = FormatSchemaFilter(schemas);

        var sql = $@"
            SELECT n.nspname,
                   t.tgname,
                   c.relname AS table_name,
                   pg_get_triggerdef(t.oid, true) AS definition
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE NOT t.tgisinternal
              AND {filter}
            ORDER BY n.nspname, t.tgname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var tableName = reader.GetString(2);
            var definition = reader.GetString(3);

            var ddl = $"-- Trigger on table: {QuoteId(schema)}.{QuoteId(tableName)}\n" +
                      definition.TrimEnd();
            if (!ddl.EndsWith(";"))
                ddl += ";";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Trigger,
                DdlScript = ddl
            });
        }

        return results;
    }

    private static string QuoteId(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
