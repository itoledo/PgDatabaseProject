namespace PgDatabaseProject.Core.Models;

public sealed class ImportProgress
{
    public required string CurrentObject { get; init; }
    public required DatabaseObjectType ObjectType { get; init; }
    public required int ProcessedCount { get; init; }
    public required int TotalCount { get; init; }
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }
}
