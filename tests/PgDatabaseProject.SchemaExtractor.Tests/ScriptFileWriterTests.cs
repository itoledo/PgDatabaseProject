using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Tests;

public sealed class ScriptFileWriterTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptFileWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pgproj_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WriteAsync_CreatesCorrectFolderStructure()
    {
        var writer = new ScriptFileWriter();
        var objects = new List<DatabaseObject>
        {
            new()
            {
                Name = "users",
                Schema = "public",
                ObjectType = DatabaseObjectType.Table,
                DdlScript = "CREATE TABLE public.users (id serial PRIMARY KEY);"
            },
            new()
            {
                Name = "active_users",
                Schema = "public",
                ObjectType = DatabaseObjectType.View,
                DdlScript = "CREATE VIEW public.active_users AS SELECT * FROM public.users;"
            },
            new()
            {
                Name = "uuid-ossp",
                Schema = "public",
                ObjectType = DatabaseObjectType.Extension,
                DdlScript = "CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";"
            }
        };

        var options = new ImportOptions { OverwriteExisting = true };
        await writer.WriteAsync(_tempDir, objects, options);

        Assert.True(File.Exists(Path.Combine(_tempDir, "Tables", "public", "users.pgsql")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Views", "public", "active_users.pgsql")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "Extensions", "uuid-ossp.pgsql")));
    }

    [Fact]
    public async Task WriteAsync_SkipsExistingWhenOverwriteIsFalse()
    {
        var writer = new ScriptFileWriter();
        var filePath = Path.Combine(_tempDir, "Tables", "public", "users.pgsql");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "-- original content");

        var objects = new List<DatabaseObject>
        {
            new()
            {
                Name = "users",
                Schema = "public",
                ObjectType = DatabaseObjectType.Table,
                DdlScript = "CREATE TABLE public.users (id serial PRIMARY KEY);"
            }
        };

        var options = new ImportOptions { OverwriteExisting = false };
        await writer.WriteAsync(_tempDir, objects, options);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("-- original content", content);
    }

    [Fact]
    public async Task WriteAsync_IncludesDropWhenRequested()
    {
        var writer = new ScriptFileWriter();
        var objects = new List<DatabaseObject>
        {
            new()
            {
                Name = "users",
                Schema = "public",
                ObjectType = DatabaseObjectType.Table,
                DdlScript = "CREATE TABLE public.users (id serial PRIMARY KEY);"
            }
        };

        var options = new ImportOptions { IncludeDropIfExists = true, OverwriteExisting = true };
        await writer.WriteAsync(_tempDir, objects, options);

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "Tables", "public", "users.pgsql"));
        Assert.Contains("DROP TABLE IF EXISTS", content);
        Assert.Contains("CREATE TABLE", content);
    }
}
