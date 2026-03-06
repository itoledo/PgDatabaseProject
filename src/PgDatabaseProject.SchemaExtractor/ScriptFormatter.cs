using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor;

public static class ScriptFormatter
{
    public static string FormatScript(DatabaseObject obj, bool includeDropIfExists)
    {
        var header = $"-- Object: {obj.Schema}.{obj.Name}\n" +
                     $"-- Type: {obj.ObjectType}\n" +
                     $"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n\n";

        var drop = includeDropIfExists
            ? GetDropStatement(obj) + "\n\n"
            : "";

        return header + drop + obj.DdlScript + "\n";
    }

    private static string GetDropStatement(DatabaseObject obj)
    {
        var qualifiedName = obj.ObjectType == DatabaseObjectType.Extension || obj.ObjectType == DatabaseObjectType.Schema
            ? QuoteId(obj.Name)
            : $"{QuoteId(obj.Schema)}.{QuoteId(obj.Name)}";

        return obj.ObjectType switch
        {
            DatabaseObjectType.Schema => $"DROP SCHEMA IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.Extension => $"DROP EXTENSION IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.Table => $"DROP TABLE IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.View => $"DROP VIEW IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.Function => $"DROP FUNCTION IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.StoredProcedure => $"DROP PROCEDURE IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.Sequence => $"DROP SEQUENCE IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.Type => $"DROP TYPE IF EXISTS {qualifiedName} CASCADE;",
            DatabaseObjectType.Trigger => $"DROP TRIGGER IF EXISTS {QuoteId(obj.Name)} ON {QuoteId(obj.Schema)} CASCADE;",
            DatabaseObjectType.Index => $"DROP INDEX IF EXISTS {qualifiedName};",
            _ => $"-- DROP not supported for {obj.ObjectType}"
        };
    }

    private static string QuoteId(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
