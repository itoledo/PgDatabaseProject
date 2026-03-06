using Npgsql;
using PgDatabaseProject.Core.Interfaces;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public abstract class BaseExtractor : IObjectExtractor
{
    protected readonly string ConnectionString;

    protected BaseExtractor(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public abstract DatabaseObjectType ObjectType { get; }

    public abstract Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default);

    protected NpgsqlConnection OpenConnection()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    protected string FormatSchemaFilter(IReadOnlyList<string> schemas)
    {
        if (schemas.Count == 0)
            return "n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')";

        var quoted = string.Join(", ", schemas.Select(s => $"'{EscapeSql(s)}'"));
        return $"n.nspname IN ({quoted})";
    }

    protected string FormatSchemaFilterSimple(IReadOnlyList<string> schemas, string column = "schemaname")
    {
        if (schemas.Count == 0)
            return $"{column} NOT IN ('pg_catalog', 'information_schema', 'pg_toast')";

        var quoted = string.Join(", ", schemas.Select(s => $"'{EscapeSql(s)}'"));
        return $"{column} IN ({quoted})";
    }

    protected static string EscapeSql(string value) => value.Replace("'", "''");
}
