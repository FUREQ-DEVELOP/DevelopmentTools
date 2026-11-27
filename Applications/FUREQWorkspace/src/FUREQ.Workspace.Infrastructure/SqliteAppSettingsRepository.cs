using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed class SqliteAppSettingsRepository(WorkspaceDatabase database) : IAppSettingsRepository
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is string value ? value : null;
    }

    public async Task SetAsync(AppSetting setting, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setting.Key);

        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSettings (Key, Value, UpdatedAtUtc)
            VALUES ($key, $value, $updatedAtUtc)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$key", setting.Key);
        command.Parameters.AddWithValue("$value", setting.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
