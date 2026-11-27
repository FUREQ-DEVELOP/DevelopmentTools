using System.Globalization;
using System.Text.Json;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using Microsoft.Data.Sqlite;

namespace FUREQ.Workspace.Infrastructure;

public sealed class SqliteSetupProgressStore(WorkspaceDatabase database) : ISetupProgressStore
{
    public async Task SavePlanAsync(WorkspaceSetupPlan plan, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SetupPlan (PlanId, WorkspaceProfileId, Data, UpdatedAtUtc)
            VALUES ($plan, $profile, $data, $updated)
            ON CONFLICT(PlanId) DO UPDATE SET Data = excluded.Data, UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$plan", plan.Id.ToString("D"));
        command.Parameters.AddWithValue("$profile", plan.WorkspaceProfileId.ToString("D"));
        command.Parameters.AddWithValue("$data", JsonSerializer.Serialize(plan));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkspaceSetupPlan?> GetLatestPlanAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM SetupPlan WHERE WorkspaceProfileId = $profile ORDER BY UpdatedAtUtc DESC LIMIT 1;";
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        var data = await command.ExecuteScalarAsync(cancellationToken) as string;
        return data is null ? null : JsonSerializer.Deserialize<WorkspaceSetupPlan>(data)
            ?? throw new InvalidDataException("保存済みSetup Planを読み取れません。");
    }

    public async Task SaveAsync(SetupItemProgress progress, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SetupProgress (PlanId, FullName, TargetPath, State, Message, UpdatedAtUtc)
            VALUES ($plan, $name, $path, $state, $message, $updated)
            ON CONFLICT(PlanId, FullName) DO UPDATE SET
                TargetPath = excluded.TargetPath, State = excluded.State,
                Message = excluded.Message, UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$plan", progress.PlanId.ToString("D"));
        command.Parameters.AddWithValue("$name", progress.FullName);
        command.Parameters.AddWithValue("$path", progress.TargetPath);
        command.Parameters.AddWithValue("$state", progress.State.ToString());
        command.Parameters.AddWithValue("$message", (object?)progress.Message ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", progress.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SetupItemProgress>> GetAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT PlanId, FullName, TargetPath, State, Message, UpdatedAtUtc FROM SetupProgress WHERE PlanId = $plan ORDER BY FullName COLLATE NOCASE;";
        command.Parameters.AddWithValue("$plan", planId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var progress = new List<SetupItemProgress>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!Enum.TryParse<SetupItemState>(reader.GetString(3), out var state))
                throw new InvalidDataException("セットアップ進捗の状態値が不正です。");
            progress.Add(new SetupItemProgress(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), state,
                reader.IsDBNull(4) ? null : reader.GetString(4), DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        }
        return progress;
    }
}
