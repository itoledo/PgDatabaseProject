using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.Core.Interfaces;

public interface IObjectExtractor
{
    DatabaseObjectType ObjectType { get; }
    Task<IReadOnlyList<DatabaseObject>> ExtractAsync(IReadOnlyList<string> schemas, CancellationToken ct = default);
}
