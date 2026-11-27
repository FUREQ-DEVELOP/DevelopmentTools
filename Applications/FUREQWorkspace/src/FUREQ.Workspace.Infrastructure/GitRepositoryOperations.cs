using System.Text;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed class GitRepositoryOperations(
    IGitProcessRunner runner,
    IGitRemotePolicy remotePolicy,
    IGitWriteIdentityValidator writeIdentityValidator) : IGitRepositoryOperations
{
    private static readonly TimeSpan LocalCommandTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan NetworkCommandTimeout = TimeSpan.FromMinutes(5);

    public async Task<GitRepositorySnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        var status = await RunCheckedAsync(repository.Path, ["status", "--porcelain=v2", "--branch", "-z"],
            LocalCommandTimeout, "status", cancellationToken, disableFsMonitor: true);
        return GitStatusParser.Parse(repository.FullName, status.StandardOutput);
    }

    public async Task<IReadOnlyList<string>> GetBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        var result = await RunCheckedAsync(repository.Path,
            ["for-each-ref", "--format=%(refname:short)", "refs/heads"], LocalCommandTimeout, "branch list", cancellationToken);
        return result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task CreateBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default)
    {
        if (!IsValidBranchName(branchName)) throw new ArgumentException("ブランチ名に使用できない文字があります。", nameof(branchName));
        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        var snapshot = await GetStatusAsync(repository.Path, cancellationToken);
        if (snapshot.IsDirty || snapshot.HasConflicts || string.IsNullOrEmpty(snapshot.Branch))
            throw new InvalidOperationException("変更があるかdetached状態のため、新しいブランチを作成できません。");
        await RunCheckedAsync(repository.Path, ["switch", "--create", branchName], LocalCommandTimeout, "branch create", cancellationToken);
    }

    public async Task<GitRepositorySnapshot> FetchAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        await RunCheckedAsync(repository.Path, ["fetch", "origin"], NetworkCommandTimeout, "fetch", cancellationToken,
            useGitCredentialManager: true);
        return await GetStatusAsync(repository.Path, cancellationToken);
    }

    public async Task<GitRepositorySnapshot> PullFastForwardAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetStatusAsync(repositoryPath, cancellationToken);
        if (snapshot.IsDirty || snapshot.HasConflicts)
            throw new InvalidOperationException("未コミット変更または競合があるためPullを中止しました。先に状態を確認してください。");
        if (string.IsNullOrEmpty(snapshot.Branch) || string.IsNullOrEmpty(snapshot.Upstream))
            throw new InvalidOperationException("現在のブランチに追跡先がありません。");
        const string remotePrefix = "origin/";
        if (!snapshot.Upstream.StartsWith(remotePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("追跡先がoriginではないためPullを中止しました。");

        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        await RunCheckedAsync(repository.Path,
            ["pull", "--ff-only", "origin", snapshot.Upstream[remotePrefix.Length..]],
            NetworkCommandTimeout, "fast-forward pull", cancellationToken, useGitCredentialManager: true);
        return await GetStatusAsync(repository.Path, cancellationToken);
    }

    public async Task<string> CommitStagedChangesAsync(string repositoryPath, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200 || message.Contains('\r') || message.Contains('\n'))
            throw new ArgumentException("コミットメッセージは1行、1〜200文字で入力してください。", nameof(message));
        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        var snapshot = await GetStatusAsync(repository.Path, cancellationToken);
        if (snapshot.HasConflicts || string.IsNullOrEmpty(snapshot.Branch) || IsProtectedBranch(snapshot.Branch))
            throw new InvalidOperationException("競合中、detached、または既定ブランチではコミットできません。");
        var staged = await runner.RunAsync(repository.Path, ["diff", "--cached", "--quiet"], LocalCommandTimeout,
            cancellationToken, disableHooks: true, disableFsMonitor: true);
        if (staged.ExitCode == 0) throw new InvalidOperationException("コミット対象がありません。GitHub Desktop等で変更をステージしてください。");
        if (staged.ExitCode != 1) throw new InvalidOperationException("ステージ済み変更を確認できませんでした。");

        var result = await RunCheckedAsync(repository.Path, ["commit", "--no-verify", "--message", message.Trim()],
            LocalCommandTimeout, "commit", cancellationToken);
        return result.StandardOutput.Trim();
    }

    public async Task PushAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetStatusAsync(repositoryPath, cancellationToken);
        if (string.IsNullOrEmpty(snapshot.Branch) || IsProtectedBranch(snapshot.Branch))
            throw new InvalidOperationException("detached状態または既定ブランチからはPushできません。");
        if (!IsValidBranchName(snapshot.Branch)) throw new InvalidOperationException("現在のブランチ名を検証できません。");
        await writeIdentityValidator.ValidateAsync(repositoryPath, cancellationToken);
        var repository = await ResolveRepositoryAsync(repositoryPath, cancellationToken);
        await RunCheckedAsync(repository.Path, ["push", "--set-upstream", "origin", snapshot.Branch],
            NetworkCommandTimeout, "push", cancellationToken, useGitCredentialManager: true);
    }

    private async Task<ResolvedRepository> ResolveRepositoryAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        if (!Directory.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("ローカルリポジトリのパスがありません、またはリンクされています。");
        var gitMarker = Path.Combine(fullPath, ".git");
        if (!Directory.Exists(gitMarker) && !File.Exists(gitMarker))
            throw new InvalidOperationException("選択したフォルダーにGitリポジトリがありません。");

        var topLevel = await RunCheckedAsync(fullPath, ["rev-parse", "--show-toplevel"], LocalCommandTimeout, "repository check", cancellationToken);
        var canonicalTopLevel = Path.TrimEndingDirectorySeparator(Path.GetFullPath(topLevel.StandardOutput.Trim()));
        if (!string.Equals(fullPath, canonicalTopLevel, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("選択したフォルダーがリポジトリのルートではありません。");

        var remote = await RunCheckedAsync(fullPath, ["remote", "get-url", "origin"], LocalCommandTimeout, "origin check", cancellationToken);
        var fullName = remotePolicy.GetRepositoryFullName(remote.StandardOutput.Trim());
        return new ResolvedRepository(fullPath, fullName);
    }

    private async Task<GitCommandResult> RunCheckedAsync(string path, IReadOnlyList<string> arguments,
        TimeSpan timeout, string operation, CancellationToken cancellationToken, bool disableFsMonitor = false,
        bool useGitCredentialManager = false)
    {
        var result = await runner.RunAsync(path, arguments, timeout, cancellationToken,
            disableHooks: true, disableFsMonitor: disableFsMonitor, useGitCredentialManager: useGitCredentialManager);
        if (result.ExitCode != 0) throw new InvalidOperationException($"Git {operation} に失敗しました (exit {result.ExitCode})。");
        return result;
    }

    private static bool IsProtectedBranch(string branch) =>
        string.Equals(branch, "main", StringComparison.OrdinalIgnoreCase)
        || string.Equals(branch, "master", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidBranchName(string branch) =>
        !string.IsNullOrWhiteSpace(branch)
        && branch.Length <= 100
        && branch[0] != '-'
        && !branch.StartsWith('/')
        && !branch.EndsWith('/')
        && !branch.EndsWith('.')
        && !branch.Contains("..", StringComparison.Ordinal)
        && !branch.Contains("@{", StringComparison.Ordinal)
        && !branch.Contains("//", StringComparison.Ordinal)
        && branch.Split('/').All(segment => segment.Length > 0 && !segment.StartsWith('.') && !segment.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        && branch.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/');

    private sealed record ResolvedRepository(string Path, string FullName);
}

public static class GitStatusParser
{
    public static GitRepositorySnapshot Parse(string repositoryFullName, string output)
    {
        var branch = "";
        string? upstream = null;
        var ahead = 0;
        var behind = 0;
        var paths = new List<string>();
        var conflicts = false;
        var records = output.Split('\0');
        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = record[14..] == "(detached)" ? "" : record[14..];
                continue;
            }
            if (record.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                upstream = record[18..];
                continue;
            }
            if (record.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                foreach (var part in record[12..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.StartsWith('+')) int.TryParse(part.AsSpan(1), out ahead);
                    if (part.StartsWith('-')) int.TryParse(part.AsSpan(1), out behind);
                }
                continue;
            }
            if (record.Length == 0 || record[0] == '#') continue;
            if (record.StartsWith("u ", StringComparison.Ordinal)) conflicts = true;
            paths.Add(ParsePath(record));
            if (record.StartsWith("2 ", StringComparison.Ordinal)) index++;
        }

        return new GitRepositorySnapshot(repositoryFullName, branch, upstream, ahead, behind,
            paths.Count > 0, conflicts, paths);
    }

    private static string ParsePath(string record)
    {
        if (record.StartsWith("? ", StringComparison.Ordinal)) return record[2..];
        var fieldLimit = record.StartsWith("2 ", StringComparison.Ordinal) ? 10
            : record.StartsWith("u ", StringComparison.Ordinal) ? 11
            : 9;
        var fields = record.Split(' ', fieldLimit);
        return fields.Length == 0 ? record : fields[^1];
    }
}
