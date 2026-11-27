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
            Assert.Equal(1L, await command.ExecuteScalarAsync(cancellationToken));
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
}
