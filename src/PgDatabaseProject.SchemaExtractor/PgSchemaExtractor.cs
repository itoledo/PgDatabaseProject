using Npgsql;
using PgDatabaseProject.Core.Interfaces;
using PgDatabaseProject.Core.Models;
using PgDatabaseProject.SchemaExtractor.Extractors;

namespace PgDatabaseProject.SchemaExtractor;

public sealed class PgSchemaExtractor : ISchemaExtractor, IDisposable
{
    private readonly string _connectionString;
    private readonly Dictionary<DatabaseObjectType, IObjectExtractor> _extractors;

    public PgSchemaExtractor(ConnectionSettings settings)
        : this(settings.ToConnectionString())
    {
    }

    public PgSchemaExtractor(string connectionString)
    {
        _connectionString = connectionString;
        _extractors = CreateExtractors();
    }

    private Dictionary<DatabaseObjectType, IObjectExtractor> CreateExtractors()
    {
        var list = new IObjectExtractor[]
        {
            new SchemaObjectExtractor(_connectionString),
            new ExtensionExtractor(_connectionString),
            new SequenceExtractor(_connectionString),
            new TypeExtractor(_connectionString),
            new TableExtractor(_connectionString),
            new ViewExtractor(_connectionString),
            FunctionExtractor.ForFunctions(_connectionString),
            FunctionExtractor.ForProcedures(_connectionString),
            new TriggerExtractor(_connectionString),
            new IndexExtractor(_connectionString),
        };

        return list.ToDictionary(e => e.ObjectType);
    }

    public async Task TestConnectionAsync(CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetSchemasAsync(CancellationToken ct = default)
    {
        var schemas = new List<string>();

        var sql = @"
            SELECT nspname
            FROM pg_namespace
            WHERE nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
              AND nspname NOT LIKE 'pg_temp_%'
              AND nspname NOT LIKE 'pg_toast_temp_%'
            ORDER BY nspname";

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            schemas.Add(reader.GetString(0));

        return schemas;
    }

    public async Task<IReadOnlyList<DatabaseObject>> ExtractObjectsAsync(
        ImportOptions options,
        IProgress<ImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var allObjects = new List<DatabaseObject>();
        var objectTypes = options.ObjectTypes.Count > 0
            ? options.ObjectTypes
            : _extractors.Keys.ToList();

        var totalTypes = objectTypes.Count;
        var processedTypes = 0;

        foreach (var objectType in objectTypes)
        {
            ct.ThrowIfCancellationRequested();

            if (!_extractors.TryGetValue(objectType, out var extractor))
                continue;

            progress?.Report(new ImportProgress
            {
                CurrentObject = $"Extracting {objectType}s...",
                ObjectType = objectType,
                ProcessedCount = processedTypes,
                TotalCount = totalTypes
            });

            try
            {
                var objects = await extractor.ExtractAsync(options.Schemas, ct);
                allObjects.AddRange(objects);
            }
            catch (Exception ex)
            {
                progress?.Report(new ImportProgress
                {
                    CurrentObject = $"Error extracting {objectType}s",
                    ObjectType = objectType,
                    ProcessedCount = processedTypes,
                    TotalCount = totalTypes,
                    IsError = true,
                    ErrorMessage = ex.Message
                });
            }

            processedTypes++;
        }

        progress?.Report(new ImportProgress
        {
            CurrentObject = "Done",
            ObjectType = DatabaseObjectType.Schema,
            ProcessedCount = totalTypes,
            TotalCount = totalTypes
        });

        return allObjects;
    }

    public void Dispose()
    {
        // No unmanaged resources to dispose with connection-per-query approach
    }
}
