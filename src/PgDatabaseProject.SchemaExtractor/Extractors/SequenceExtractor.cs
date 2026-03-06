using Npgsql;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Extractors;

public sealed class SequenceExtractor : BaseExtractor
{
    public SequenceExtractor(string connectionString) : base(connectionString) { }

    public override DatabaseObjectType ObjectType => DatabaseObjectType.Sequence;

    public override async Task<IReadOnlyList<DatabaseObject>> ExtractAsync(
        IReadOnlyList<string> schemas, CancellationToken ct = default)
    {
        var results = new List<DatabaseObject>();
        var filter = FormatSchemaFilterSimple(schemas, "sequence_schema");

        var sql = $@"
            SELECT sequence_schema, sequence_name, data_type,
                   start_value, minimum_value, maximum_value,
                   increment, cycle_option
            FROM information_schema.sequences
            WHERE {filter}
            ORDER BY sequence_schema, sequence_name";

        using var conn = OpenConnection();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var dataType = reader.GetString(2);
            var startValue = reader.GetString(3);
            var minValue = reader.GetString(4);
            var maxValue = reader.GetString(5);
            var increment = reader.GetString(6);
            var cycle = reader.GetString(7);

            var ddl = $"CREATE SEQUENCE {QuoteIdentifier(schema)}.{QuoteIdentifier(name)}\n" +
                      $"    AS {dataType}\n" +
                      $"    INCREMENT BY {increment}\n" +
                      $"    MINVALUE {minValue}\n" +
                      $"    MAXVALUE {maxValue}\n" +
                      $"    START WITH {startValue}\n" +
                      (cycle == "YES" ? "    CYCLE" : "    NO CYCLE") + ";";

            results.Add(new DatabaseObject
            {
                Name = name,
                Schema = schema,
                ObjectType = DatabaseObjectType.Sequence,
                DdlScript = ddl
            });
        }

        return results;
    }

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
