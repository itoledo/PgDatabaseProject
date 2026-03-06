namespace PgDatabaseProject.Core.Models;

public sealed class ImportOptions
{
    public IReadOnlyList<string> Schemas { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DatabaseObjectType> ObjectTypes { get; init; } = Array.Empty<DatabaseObjectType>();
    public bool IncludeDropIfExists { get; init; }
    public bool OverwriteExisting { get; init; } = true;
}
