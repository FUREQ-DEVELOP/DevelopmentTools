using Microsoft.Data.Sqlite;

namespace FUREQ.Workspace.Infrastructure;

public sealed class WorkspaceDatabase
{
    public WorkspaceDatabase(string? databasePath = null)
    {
        DatabasePath = databasePath ?? GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        }.ToString();
    }

    public string DatabasePath { get; }

    internal string ConnectionString { get; }

    internal SqliteConnection CreateConnection() => new(ConnectionString);

    private static string GetDefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Windows Local Application Data is unavailable.");
        }

        return Path.Combine(localAppData, "FUREQ", "FUREQWorkspace", "workspace.db");
    }
}
