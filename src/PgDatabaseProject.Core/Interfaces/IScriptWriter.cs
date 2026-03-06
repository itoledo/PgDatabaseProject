using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.Core.Interfaces;

public interface IScriptWriter
{
    Task WriteAsync(string projectRootPath, IReadOnlyList<DatabaseObject> objects, ImportOptions options, CancellationToken ct = default);
}
