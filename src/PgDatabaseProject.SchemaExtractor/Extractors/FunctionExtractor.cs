using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class FunctionExtractor : BaseExtractor
{
    private readonly DatabaseObjectType _objectType;
    private readonly string _prokindFilter;

    private FunctionExtractor(string connectionString, DatabaseObjectType objectType, string prokindFilter)
        : base(connectionString)
    {
        _objectType = objectType;
        _prokindFilter = prokindFilter;
    }

    public static FunctionExtractor ForFunctions(string connectionString) =>
        new(connectionString, DatabaseObjectType.Function, "'f', 'w'");

    public static FunctionExtractor ForProcedures(string connectionString) =>
        new(connectionString, DatabaseObjectType.StoredProcedure, "'p'");

    public override DatabaseObjectType ObjectType => _objectType;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();
        var filter = FormatSchemaFilter(schemas);

        var sql = $@"
            SELECT n.nspname, p.proname, pg_get_functiondef(p.oid) AS definition
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE {filter}
              AND p.prokind IN ({_prokindFilter})
            ORDER BY n.nspname, p.proname";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var definition = reader.IsDBNull(2) ? null : reader.GetString(2);

            if (definition == null)
                continue;

            var ddl = definition.TrimEnd();
            if (!ddl.EndsWith(";"))
                ddl += ";";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = _objectType,
                DdlScript = ddl
            });
        }

        return results;
    }
}
