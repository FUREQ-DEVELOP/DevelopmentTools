using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task Initialize_is_idempotent_and_settings_round_trip()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"fureq-{Guid.NewGuid():N}.db");
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var database = new WorkspaceDatabase(databasePath);
            var initializer = new DatabaseInitializer(database);
            await initializer.InitializeAsync(cancellationToken);
            await initializer.InitializeAsync(cancellationToken);

            var repository = new SqliteAppSettingsRepository(database);
            await repository.SetAsync(new AppSetting("locale", "ja-JP"), cancellationToken);

            Assert.Equal("ja-JP", await repository.GetAsync("locale", cancellationToken));
            await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.Equal(2L, await command.ExecuteScalarAsync(cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                var path = databasePath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    [Fact]
    public async Task Upgrades_schema_v1_to_v2_without_losing_existing_settings()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"fureq-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE AppSettings (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL);
                    CREATE TABLE Workspace (Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, RootPath TEXT NOT NULL, CreatedAtUtc TEXT NOT NULL);
                    INSERT INTO AppSettings VALUES ('theme', 'dark', '2026-01-01T00:00:00Z');
                    PRAGMA user_version = 1;
                    """;
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var database = new WorkspaceDatabase(databasePath);
            await new DatabaseInitializer(database).InitializeAsync(TestContext.Current.CancellationToken);
            var settings = new SqliteAppSettingsRepository(database);

            Assert.Equal("dark", await settings.GetAsync("theme", TestContext.Current.CancellationToken));
            Assert.Equal(2L, await GetSchemaVersionAsync(databasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                var path = databasePath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    private static async Task<long> GetSchemaVersionAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
