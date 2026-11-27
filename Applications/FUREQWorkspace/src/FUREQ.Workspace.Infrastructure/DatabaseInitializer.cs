using Microsoft.Data.Sqlite;

namespace FUREQ.Workspace.Infrastructure;

public sealed class DatabaseInitializer(WorkspaceDatabase database)
{
    private const int CurrentSchemaVersion = 3;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using (var pragmas = connection.CreateCommand())
        {
            pragmas.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
            await pragmas.ExecuteNonQueryAsync(cancellationToken);
        }

        var version = await GetSchemaVersionAsync(connection, cancellationToken);
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Database schema version {version} is newer than this app supports.");
        }

        if (version < 1)
        {
            await ApplyVersion1Async(connection, cancellationToken);
        }

        if (version < 2)
        {
            await ApplyVersion2Async(connection, cancellationToken);
        }

        if (version < 3)
        {
            await ApplyVersion3Async(connection, cancellationToken);
        }
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ApplyVersion1Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT NOT NULL PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Workspace (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                RootPath TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            PRAGMA user_version = 1;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ApplyVersion2Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS WorkspaceProfile (
                Id TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL,
                Role TEXT NOT NULL,
                RootPath TEXT NOT NULL COLLATE NOCASE UNIQUE,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS LocalRepository (
                Id TEXT NOT NULL PRIMARY KEY,
                WorkspaceProfileId TEXT NOT NULL REFERENCES WorkspaceProfile(Id) ON DELETE CASCADE,
                DirectoryName TEXT NOT NULL,
                Path TEXT NOT NULL COLLATE NOCASE UNIQUE,
                IsPresent INTEGER NOT NULL DEFAULT 1,
                LastScannedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_LocalRepository_WorkspaceProfileId
                ON LocalRepository(WorkspaceProfileId);
            PRAGMA user_version = 2;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ApplyVersion3Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS SetupProgress (
                PlanId TEXT NOT NULL,
                FullName TEXT NOT NULL COLLATE NOCASE,
                TargetPath TEXT NOT NULL,
                State TEXT NOT NULL,
                Message TEXT,
                UpdatedAtUtc TEXT NOT NULL,
                PRIMARY KEY (PlanId, FullName)
            );
            CREATE INDEX IF NOT EXISTS IX_SetupProgress_PlanId ON SetupProgress(PlanId);
            CREATE TABLE IF NOT EXISTS SetupPlan (
                PlanId TEXT NOT NULL PRIMARY KEY,
                WorkspaceProfileId TEXT NOT NULL,
                Data TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SetupPlan_WorkspaceProfileId ON SetupPlan(WorkspaceProfileId, UpdatedAtUtc DESC);
            PRAGMA user_version = 3;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
