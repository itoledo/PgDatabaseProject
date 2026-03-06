namespace PgDatabaseProject.Core.Models;

public sealed class DatabaseObject
{
    public required string Name { get; init; }
    public required string Schema { get; init; }
    public required DatabaseObjectType ObjectType { get; init; }
    public required string DdlScript { get; init; }

    public string FileName => ObjectType == DatabaseObjectType.Extension || ObjectType == DatabaseObjectType.Schema
        ? $"{Name}.pgsql"
        : $"{Name}.pgsql";

    public string FolderName => ObjectType switch
    {
        DatabaseObjectType.Schema => "Schemas",
        DatabaseObjectType.Extension => "Extensions",
        DatabaseObjectType.Sequence => "Sequences",
        DatabaseObjectType.Type => "Types",
        DatabaseObjectType.Table => "Tables",
        DatabaseObjectType.View => "Views",
        DatabaseObjectType.Function => "Functions",
        DatabaseObjectType.StoredProcedure => "StoredProcedures",
        DatabaseObjectType.Trigger => "Triggers",
        DatabaseObjectType.Index => "Indexes",
        _ => "Other"
    };

    /// <summary>
    /// Relative path within the project folder: e.g. "Tables/public/users.pgsql"
    /// </summary>
    public string RelativePath => ObjectType == DatabaseObjectType.Extension || ObjectType == DatabaseObjectType.Schema
        ? Path.Combine(FolderName, FileName)
        : Path.Combine(FolderName, Schema, FileName);
}
