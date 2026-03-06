using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class IndexExtractor : BaseExtractor
{
    public IndexExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Index;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();
        var filter = FormatSchemaFilterSimple(schemas);

        var sql = $@"
            SELECT i.schemaname, i.indexname, i.tablename, i.indexdef
            FROM pg_indexes i
            WHERE {filter}
              AND i.indexname NOT IN (
                  SELECT conname FROM pg_constraint
                  WHERE contype IN ('p', 'u')
              )
            ORDER BY i.schemaname, i.indexname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var tableName = reader.GetString(2);
            var indexDef = reader.GetString(3);

            var ddl = $"-- Index on table: {QuoteId(schema)}.{QuoteId(tableName)}\n" +
                      indexDef.TrimEnd() + ";";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Index,
                DdlScript = ddl
            });
        }

        return results;
    }

    private static string QuoteId(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
