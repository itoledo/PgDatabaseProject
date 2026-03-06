using System;
using System.Collections.Generic;

namespace PgDatabaseProject.Extension.Services;

internal sealed class ConnectionEntry
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "";
    public string SslMode { get; set; } = "Prefer";

    public string DisplayName => $"{Username}@{Host}:{Port}/{Database}";

    public string ToConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};SSL Mode={SslMode}";

    public static ConnectionEntry? ParseConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var entry = new ConnectionEntry();
        var foundHost = false;

        foreach (var part in connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = part.Substring(0, eqIndex).Trim();
            var value = part.Substring(eqIndex + 1).Trim();

            switch (key.ToLowerInvariant())
            {
                case "host":
                case "server":
                    entry.Host = value;
                    foundHost = true;
                    break;
                case "port":
                    if (int.TryParse(value, out var port)) entry.Port = port;
                    break;
                case "database":
                case "db":
                case "initial catalog":
                    entry.Database = value;
                    break;
                case "username":
                case "user id":
                case "user":
                case "uid":
                    entry.Username = value;
                    break;
                case "password":
                case "pwd":
                    entry.Password = value;
                    break;
                case "ssl mode":
                case "sslmode":
                    entry.SslMode = value;
                    break;
            }
        }

        return foundHost ? entry : null;
    }

    public ConnectionEntry Clone() => new()
    {
        Host = Host,
        Port = Port,
        Database = Database,
        Username = Username,
        Password = Password,
        SslMode = SslMode
    };
}
