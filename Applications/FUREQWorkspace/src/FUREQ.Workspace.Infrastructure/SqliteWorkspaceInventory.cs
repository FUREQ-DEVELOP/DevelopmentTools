using System.Globalization;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using Microsoft.Data.Sqlite;

namespace FUREQ.Workspace.Infrastructure;

public sealed class SqliteWorkspaceInventory(WorkspaceDatabase database) : IWorkspaceInventory
{
    public async Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Role, RootPath, CreatedAtUtc FROM WorkspaceProfile ORDER BY Name COLLATE NOCASE;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var profiles = new List<WorkspaceProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(new WorkspaceProfile(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
        }

        return profiles;
    }

    public async Task<WorkspaceProfile> CreateProfileAsync(string name, string role, string rootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("プロファイル名を入力してください。", nameof(name));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("班または職種を入力してください。", nameof(role));
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("保存先フォルダーを入力してください。", nameof(rootPath));

        var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath.Trim()));
        if (!Directory.Exists(canonicalPath)) throw new DirectoryNotFoundException("選択した保存先フォルダーがありません。");
        if ((File.GetAttributes(canonicalPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("保存先にリンクされたフォルダーは指定できません。");
        }

        var profile = new WorkspaceProfile(Guid.NewGuid(), name.Trim(), role.Trim(), canonicalPath, DateTimeOffset.UtcNow);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO WorkspaceProfile (Id, Name, Role, RootPath, CreatedAtUtc)
            VALUES ($id, $name, $role, $path, $created);
            """;
        command.Parameters.AddWithValue("$id", profile.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$role", profile.Role);
        command.Parameters.AddWithValue("$path", profile.RootPath);
        command.Parameters.AddWithValue("$created", profile.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("この保存先はすでに別のプロファイルで使われています。", exception);
        }

        return profile;
    }

    public async Task<IReadOnlyList<LocalRepositoryLocation>> GetRepositoriesAsync(Guid workspaceProfileId, CancellationToken cancellationToken = default)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, WorkspaceProfileId, DirectoryName, Path, IsPresent, LastScannedAtUtc
            FROM LocalRepository WHERE WorkspaceProfileId = $id ORDER BY DirectoryName COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$id", workspaceProfileId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var repositories = new List<LocalRepositoryLocation>();
        while (await reader.ReadAsync(cancellationToken))
        {
            repositories.Add(new LocalRepositoryLocation(
                Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4) != 0, DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        }

        return repositories;
    }

    public async Task<int> ScanRepositoriesAsync(Guid workspaceProfileId, CancellationToken cancellationToken = default)
    {
        var profile = (await GetProfilesAsync(cancellationToken)).FirstOrDefault(item => item.Id == workspaceProfileId)
            ?? throw new KeyNotFoundException("ワークスペースプロファイルが見つかりません。");
        var discovered = new List<(string Name, string Path)>();
        foreach (var directory in Directory.EnumerateDirectories(profile.RootPath, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
            var gitMarker = Path.Combine(directory, ".git");
            if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker)) continue;

            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            discovered.Add((Path.GetFileName(fullPath), fullPath));
        }

        await using var connection = database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var markMissing = connection.CreateCommand())
        {
            markMissing.Transaction = (SqliteTransaction)transaction;
            markMissing.CommandText = "UPDATE LocalRepository SET IsPresent = 0 WHERE WorkspaceProfileId = $workspace;";
            markMissing.Parameters.AddWithValue("$workspace", profile.Id.ToString("D"));
            await markMissing.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var repository in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO LocalRepository (Id, WorkspaceProfileId, DirectoryName, Path, IsPresent, LastScannedAtUtc)
                VALUES ($id, $workspace, $name, $path, 1, $scanned)
                ON CONFLICT(Path) DO UPDATE SET
                    DirectoryName = excluded.DirectoryName,
                    IsPresent = 1,
                    LastScannedAtUtc = excluded.LastScannedAtUtc
                WHERE LocalRepository.WorkspaceProfileId = excluded.WorkspaceProfileId;
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("$workspace", profile.Id.ToString("D"));
            command.Parameters.AddWithValue("$name", repository.Name);
            command.Parameters.AddWithValue("$path", repository.Path);
            command.Parameters.AddWithValue("$scanned", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var count = connection.CreateCommand())
        {
            count.Transaction = (SqliteTransaction)transaction;
            count.CommandText = "SELECT COUNT(*) FROM LocalRepository WHERE WorkspaceProfileId = $workspace AND IsPresent = 1;";
            count.Parameters.AddWithValue("$workspace", profile.Id.ToString("D"));
            var result = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }
}
