namespace PgDatabaseProject.Core.Models;

public sealed class ConnectionSettings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 5432;
    public required string Database { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string SslMode { get; init; } = "Prefer";

    public string ToConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SSL Mode={SslMode}";
}
