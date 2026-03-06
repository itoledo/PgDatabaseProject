using System;

namespace PgDatabaseProject.Extension.Services;

internal sealed class ConnectionManager
{
    private static readonly ConnectionManager _instance = new();
    public static ConnectionManager Instance => _instance;

    public string? ConnectionString { get; private set; }
    public string? DatabaseName { get; private set; }
    public bool IsConnected => ConnectionString != null;

    public event EventHandler? ConnectionChanged;

    public void SetConnection(string connectionString, string databaseName)
    {
        ConnectionString = connectionString;
        DatabaseName = databaseName;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Disconnect()
    {
        ConnectionString = null;
        DatabaseName = null;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
