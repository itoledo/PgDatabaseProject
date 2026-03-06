using PgDatabaseProject.Core.Models;

namespace PgDatabaseProject.SchemaExtractor.Tests;

[Trait("Category", "Integration")]
public sealed class PgSchemaExtractorTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PgSchemaExtractorTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private PgSchemaExtractor CreateExtractor() => new(_fixture.ConnectionString);

    [Fact]
    public async Task TestConnection_Succeeds()
    {
        using var extractor = CreateExtractor();
        await extractor.TestConnectionAsync();
    }

    [Fact]
    public async Task GetSchemas_ReturnsPublicAndApp()
    {
        using var extractor = CreateExtractor();
        var schemas = await extractor.GetSchemasAsync();

        Assert.Contains("public", schemas);
        Assert.Contains("app", schemas);
    }

    [Fact]
    public async Task ExtractAll_ReturnsObjectsOfAllTypes()
    {
        using var extractor = CreateExtractor();
        var options = new ImportOptions
        {
            Schemas = new[] { "public", "app" }
        };

        var objects = await extractor.ExtractObjectsAsync(options);

        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Schema && o.Name == "app");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Table && o.Name == "users");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Table && o.Name == "orders");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Table && o.Name == "config");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.View && o.Name == "active_users");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Function && o.Name == "get_user_count");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.StoredProcedure && o.Name == "deactivate_user");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Sequence && o.Name == "order_seq");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Type && o.Name == "status_type");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Index && o.Name == "idx_users_email");
        Assert.Contains(objects, o => o.ObjectType == DatabaseObjectType.Trigger && o.Name == "trg_config_timestamp");
    }

    [Fact]
    public async Task ExtractTables_ContainsColumnsAndConstraints()
    {
        using var extractor = CreateExtractor();
        var options = new ImportOptions
        {
            Schemas = new[] { "public" },
            ObjectTypes = new[] { DatabaseObjectType.Table }
        };

        var objects = await extractor.ExtractObjectsAsync(options);
        var usersTable = objects.First(o => o.Name == "users");

        Assert.Contains("username", usersTable.DdlScript);
        Assert.Contains("email", usersTable.DdlScript);
        Assert.Contains("NOT NULL", usersTable.DdlScript);
        Assert.Contains("PRIMARY KEY", usersTable.DdlScript);
    }

    [Fact]
    public async Task ExtractFunctions_ReturnsFunctionDef()
    {
        using var extractor = CreateExtractor();
        var options = new ImportOptions
        {
            Schemas = new[] { "public" },
            ObjectTypes = new[] { DatabaseObjectType.Function }
        };

        var objects = await extractor.ExtractObjectsAsync(options);
        var func = objects.First(o => o.Name == "get_user_count");

        Assert.Contains("FUNCTION", func.DdlScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_user_count", func.DdlScript);
    }

    [Fact]
    public async Task ExtractWithFilter_OnlyReturnsRequestedTypes()
    {
        using var extractor = CreateExtractor();
        var options = new ImportOptions
        {
            Schemas = new[] { "public" },
            ObjectTypes = new[] { DatabaseObjectType.View }
        };

        var objects = await extractor.ExtractObjectsAsync(options);

        Assert.All(objects, o => Assert.Equal(DatabaseObjectType.View, o.ObjectType));
        Assert.Contains(objects, o => o.Name == "active_users");
    }

    [Fact]
    public async Task ExtractObjects_ReportsProgress()
    {
        using var extractor = CreateExtractor();
        var options = new ImportOptions
        {
            Schemas = new[] { "public" },
            ObjectTypes = new[] { DatabaseObjectType.Table, DatabaseObjectType.View }
        };

        var reports = new List<ImportProgress>();
        var progress = new Progress<ImportProgress>(p => reports.Add(p));

        await extractor.ExtractObjectsAsync(options, progress);

        await Task.Delay(100);

        Assert.NotEmpty(reports);
    }

    [Fact]
    public async Task DatabaseObject_RelativePath_IsCorrect()
    {
        using var extractor = CreateExtractor();
        var options = new ImportOptions
        {
            Schemas = new[] { "public" },
            ObjectTypes = new[] { DatabaseObjectType.Table }
        };

        var objects = await extractor.ExtractObjectsAsync(options);
        var usersTable = objects.First(o => o.Name == "users");

        var expected = Path.Combine("Tables", "public", "users.pgsql");
        Assert.Equal(expected, usersTable.RelativePath);
    }
}
