using PgDatabaseProject.Core.Interfaces;
using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor;

public sealed class ScriptFileWriter : IScriptWriter
{
    public async Task WriteAsync(
        string projectRootPath,
        IReadOnlyList<DatabaseObject> objects,
        ImportOptions options,
        CancellationToken ct = default)
    {
        foreach (var obj in objects)
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = obj.RelativePath;
            var fullPath = Path.Combine(projectRootPath, relativePath);

            if (!options.OverwriteExisting && File.Exists(fullPath))
                continue;

            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var content = ScriptFormatter.FormatScript(obj, options.IncludeDropIfExists);
            File.WriteAllText(fullPath, content);
        }
    }
}
