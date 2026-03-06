using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.Core.Interfaces;

public interface ISchemaExtractor
{
    Task<IReadOnlyList<string>> GetSchemasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseObject>> ExtractObjectsAsync(ImportOptions options, IProgress<ImportProgress>? progress = null, CancellationToken ct = default);
    Task TestConnectionAsync(CancellationToken ct = default);
}
