using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed class WorkspaceSetupService(
    IGitProcessRunner runner,
    IGitRemotePolicy remotePolicy,
    ISubmoduleService submoduleService,
    ISetupProgressStore progressStore) : IWorkspaceSetupService
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(10);
    private static readonly IReadOnlyDictionary<string, string[]> Dependencies = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["RenderingWorkspace"] = ["RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
        ["EngineWorkspace"] = ["GameEngine", "EngineEditor", "EngineHub", "DevelopmentTools", "DevelopmentDocs", "RenderingLibrary"],
        ["Game-001"] = ["Game-001-Code", "Game-001-Art", "Game-001-Design", "GameEngine", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
        ["Game-001-Code"] = ["Game-001", "GameEngine", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
        ["Game-001-Art"] = ["Game-001", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
        ["Game-001-Design"] = ["Game-001", "DevelopmentTools", "DevelopmentDocs"]
    };

    public async Task<WorkspaceSetupPlan> CreatePlanAsync(WorkspaceProfile profile, WorkspaceSetupMode mode,
        IReadOnlyList<RepositoryMetadata> repositories, LfsDownloadMode lfsMode,
        IReadOnlyCollection<string>? customRepositories = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Directory.Exists(profile.RootPath) || (File.GetAttributes(profile.RootPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("ワークスペースの保存先がありません、またはリンクされています。");
        var mapping = await ReadLayoutAsync(cancellationToken);
        var requestedNames = SelectRepositories(mode, profile.Role, customRepositories);
        requestedNames = ExpandDependencies(requestedNames);
        var metadataByName = repositories.ToDictionary(item => item.FullName, StringComparer.OrdinalIgnoreCase);
        var items = new List<WorkspaceSetupItem>();
        foreach (var name in mapping.Repositories.Where(item => requestedNames.Contains(item.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = ValidateRelativePath(name.Path);
            var target = SafeCombine(profile.RootPath, relativePath);
            var dependencies = Dependencies.TryGetValue(name.Name, out var deps) ? deps : [];
            if (!metadataByName.TryGetValue($"FUREQ-DEVELOP/{name.Name}", out var metadata))
            {
                items.Add(new WorkspaceSetupItem(0, $"FUREQ-DEVELOP/{name.Name}", "", "", relativePath, target,
                    SetupItemState.Blocked, "公開リポジトリ一覧にありません。アクセス権またはリポジトリ名を確認してください。", dependencies));
                continue;
            }

            var state = SetupItemState.Planned;
            string? message = lfsMode == LfsDownloadMode.Development ? "LFSは自動取得しません。" : "LFS見積もりを実装するまでDevelopmentモードのみ実行できます。";
            try
            {
                if (!string.Equals(remotePolicy.GetRepositoryFullName(metadata.CloneUrl), metadata.FullName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Clone URLとリポジトリ識別子が一致しません。");
                if (lfsMode != LfsDownloadMode.Development) state = SetupItemState.Blocked;
                else if (HasReparsePoint(profile.RootPath, Path.GetDirectoryName(target)!))
                {
                    state = SetupItemState.Blocked;
                    message = "保存先の親フォルダーにリンクがあります。";
                }
                else if (Directory.Exists(target) || File.Exists(target))
                {
                    if (HasReparsePoint(profile.RootPath, target))
                    {
                        state = SetupItemState.Blocked;
                        message = "保存先がリンクのため、再利用・上書きを行いません。";
                    }
                    else if (await IsMatchingRepositoryAsync(target, metadata.FullName, cancellationToken))
                    {
                        state = SetupItemState.Reuse;
                        message = "同じoriginのローカルリポジトリを再利用します。内容は変更しません。";
                    }
                    else
                    {
                        state = SetupItemState.Blocked;
                        message = "保存先が既に使われています。フォルダーを上書きしません。";
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                state = SetupItemState.Blocked;
                message = exception.Message;
            }

            items.Add(new WorkspaceSetupItem(metadata.Id, metadata.FullName, metadata.CloneUrl, metadata.DefaultBranch,
                relativePath, target, state, message, dependencies));
        }

        foreach (var requested in requestedNames.Except(mapping.Repositories.Select(item => item.Name), StringComparer.OrdinalIgnoreCase))
            items.Add(new WorkspaceSetupItem(0, $"FUREQ-DEVELOP/{requested}", "", "", "", "", SetupItemState.Blocked,
                "layout JSONにパスマッピングがありません。", []));

        var plan = new WorkspaceSetupPlan(Guid.NewGuid(), profile.Id, Path.GetFullPath(profile.RootPath), mode, lfsMode, DateTimeOffset.UtcNow, items);
        await progressStore.SavePlanAsync(plan, cancellationToken);
        return plan;
    }

    public async Task<WorkspaceSetupPlan> ExecuteAsync(WorkspaceSetupPlan plan, IProgress<SetupItemProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(plan.RootPath) || (File.GetAttributes(plan.RootPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("保存先ルートが存在しないか、リンクに置き換わっています。");
        if (plan.Blockers.Count > 0 && plan.Items.All(item => item.State is SetupItemState.Blocked or SetupItemState.Reuse))
            throw new InvalidOperationException("Setup Planに実行できる項目がありません。ブロッカーを確認してください。");
        if (plan.LfsMode != LfsDownloadMode.Development)
            throw new InvalidOperationException("現在はLFS payloadを取得しないDevelopmentモードのみ実行できます。");
        foreach (var item in plan.Items)
        {
            if (item.State is SetupItemState.Blocked or SetupItemState.Failed) continue;
            var expected = SafeCombine(plan.RootPath, ValidateRelativePath(item.RelativePath));
            if (!string.Equals(expected, Path.GetFullPath(item.TargetPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("計画の保存先が作成時から変更されました。");
            if (!string.Equals(remotePolicy.GetRepositoryFullName(item.CloneUrl), item.FullName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("計画のClone URLとリポジトリ識別子が一致しません。");
        }

        var states = new ConcurrentDictionary<string, WorkspaceSetupItem>(plan.Items.ToDictionary(item => item.FullName, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        using var concurrency = new SemaphoreSlim(3, 3);
        var tasks = plan.Items.Where(item => item.State is SetupItemState.Planned or SetupItemState.Reuse or SetupItemState.Failed or SetupItemState.Cancelled).Select(async item =>
        {
            var acquired = false;
            try
            {
                await concurrency.WaitAsync(cancellationToken);
                acquired = true;
                var result = item.State == SetupItemState.Reuse
                    ? await ReuseItemAsync(item, plan, progress, cancellationToken)
                    : await CloneItemAsync(item, plan, progress, cancellationToken);
                states[item.FullName] = result;
            }
            catch (OperationCanceledException)
            {
                states[item.FullName] = await RecordAsync(item, plan.Id, SetupItemState.Cancelled, "キャンセルされました。完了済みのリポジトリは保持されています。", progress, CancellationToken.None);
            }
            catch (Exception exception)
            {
                states[item.FullName] = await RecordAsync(item, plan.Id, SetupItemState.Failed, SafeMessage(exception), progress, CancellationToken.None);
            }
            finally { if (acquired) concurrency.Release(); }
        }).ToArray();
        await Task.WhenAll(tasks);
        var updated = plan with { Items = plan.Items.Select(item => states[item.FullName]).ToArray() };
        await progressStore.SavePlanAsync(updated, CancellationToken.None);
        return updated;
    }

    public async Task<WorkspaceSetupPlan?> GetLatestPlanAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var saved = await progressStore.GetLatestPlanAsync(profileId, cancellationToken);
        if (saved is null) return null;
        var progress = await progressStore.GetAsync(saved.Id, cancellationToken);
        var progressByName = progress.ToDictionary(item => item.FullName, StringComparer.OrdinalIgnoreCase);
        var items = new List<WorkspaceSetupItem>();
        foreach (var item in saved.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (progressByName.TryGetValue(item.FullName, out var entry)
                && entry.State is SetupItemState.Completed or SetupItemState.Reuse
                && await IsMatchingRepositoryAsync(item.TargetPath, item.FullName, cancellationToken))
            {
                items.Add(item with { State = SetupItemState.Reuse, Message = "前回の完了済みリポジトリを再利用します。" });
            }
            else if (item.State == SetupItemState.Blocked)
            {
                items.Add(item);
            }
            else
            {
                items.Add(item with { State = SetupItemState.Planned, Message = "未完了項目を再開できます。" });
            }
        }
        return saved with { Items = items };
    }

    public Task<IReadOnlyList<SetupItemProgress>> GetProgressAsync(Guid planId, CancellationToken cancellationToken = default) =>
        progressStore.GetAsync(planId, cancellationToken);

    private async Task<WorkspaceSetupItem> CloneItemAsync(WorkspaceSetupItem item, WorkspaceSetupPlan plan,
        IProgress<SetupItemProgress>? progress, CancellationToken cancellationToken)
    {
        await RecordAsync(item, plan.Id, SetupItemState.InProgress, "Staging領域へCloneしています。", progress, cancellationToken);
        var staging = item.TargetPath + $".fureq-setup-{plan.Id:N}-{item.RepositoryId}";
        var targetParent = Path.GetDirectoryName(item.TargetPath)!;
        Directory.CreateDirectory(targetParent);
        if (HasReparsePoint(plan.RootPath, targetParent)) throw new InvalidOperationException("保存先の親にリンクがあります。");
        if (Directory.Exists(item.TargetPath) || File.Exists(item.TargetPath))
        {
            if (await IsMatchingRepositoryAsync(item.TargetPath, item.FullName, cancellationToken))
                return await RecordAsync(item, plan.Id, SetupItemState.Reuse, "実行前に同じoriginのリポジトリが作られたため再利用します。", progress, cancellationToken);
            throw new InvalidOperationException("実行前に保存先が使われ始めたため、上書きせず中止しました。");
        }
        if (!IsSafeBranchName(item.DefaultBranch)) throw new InvalidOperationException("既定ブランチ名を安全に処理できません。");

        var reusableStage = false;
        if (Directory.Exists(staging) || File.Exists(staging))
        {
            if (HasReparsePoint(plan.RootPath, staging)) throw new InvalidOperationException("Staging領域がリンクに置き換わっています。");
            if (await IsMatchingRepositoryAsync(staging, item.FullName, cancellationToken))
            {
                try
                {
                    var status = await RunGitAsync(staging, ["status", "--porcelain=v2", "-z"], cancellationToken);
                    if (string.IsNullOrEmpty(status.StandardOutput))
                    {
                        await RunGitAsync(staging, ["fetch", "origin", item.DefaultBranch], cancellationToken, network: true);
                        reusableStage = true;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Keep damaged or user-modified staging contents intact and retry in a new sibling.
                }
            }
            if (!reusableStage) staging += $"-{Guid.NewGuid():N}";
        }
        if (!reusableStage)
        {
            var clone = await runner.RunAsync(plan.RootPath,
                ["clone", "--no-checkout", "--no-recurse-submodules", "--", item.CloneUrl, staging], GitTimeout,
                cancellationToken, disableHooks: true, useGitCredentialManager: true, isolateCheckoutConfiguration: true);
            if (clone.ExitCode != 0) throw new InvalidOperationException("Cloneに失敗しました。ネットワークとGitHubの公開状態を確認してください。");
        }

        await RunGitAsync(staging, ["check-ref-format", "--branch", item.DefaultBranch], cancellationToken);
        await RunGitAsync(staging, ["checkout", "--detach", $"refs/remotes/origin/{item.DefaultBranch}"], cancellationToken, isolate: true);
        var actualOrigin = await RunGitAsync(staging, ["remote", "get-url", "origin"], cancellationToken);
        if (!string.Equals(remotePolicy.GetRepositoryFullName(actualOrigin.StandardOutput.Trim()), item.FullName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Clone後のoriginが対象リポジトリと異なります。");
        var commit = await RunGitAsync(staging, ["rev-parse", "HEAD"], cancellationToken);
        var remoteCommit = await RunGitAsync(staging, ["rev-parse", $"refs/remotes/origin/{item.DefaultBranch}"], cancellationToken);
        if (!string.Equals(commit.StandardOutput.Trim(), remoteCommit.StandardOutput.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Clone後のcommit SHAを検証できませんでした。");
        await submoduleService.InitializeMissingRecursivelyAsync(staging, cancellationToken);
        if (Directory.Exists(item.TargetPath) || File.Exists(item.TargetPath))
            throw new InvalidOperationException("検証中に保存先が作成されたため、Stagingを保持して中止しました。");
        Directory.Move(staging, item.TargetPath);
        return await RecordAsync(item, plan.Id, SetupItemState.Completed, "Cloneと検証が完了しました。LFS payloadは未取得です。", progress, cancellationToken);
    }

    private async Task<WorkspaceSetupItem> ReuseItemAsync(WorkspaceSetupItem item, WorkspaceSetupPlan plan,
        IProgress<SetupItemProgress>? progress, CancellationToken cancellationToken)
    {
        if (!await IsMatchingRepositoryAsync(item.TargetPath, item.FullName, cancellationToken))
            throw new InvalidOperationException("再利用予定のリポジトリが計画と一致しなくなりました。");
        return await RecordAsync(item, plan.Id, SetupItemState.Reuse, item.Message, progress, cancellationToken);
    }

    private async Task<WorkspaceSetupItem> RecordAsync(WorkspaceSetupItem item, Guid planId, SetupItemState state, string? message,
        IProgress<SetupItemProgress>? progress, CancellationToken cancellationToken)
    {
        var entry = new SetupItemProgress(planId, item.FullName, item.TargetPath, state, message, DateTimeOffset.UtcNow);
        await progressStore.SaveAsync(entry, cancellationToken);
        progress?.Report(entry);
        return item with { State = state, Message = message };
    }

    private async Task<bool> IsMatchingRepositoryAsync(string path, string expectedFullName, CancellationToken cancellationToken)
    {
        if (HasReparsePoint(Path.GetPathRoot(Path.GetFullPath(path))!, path)) return false;
        if (!Directory.Exists(path) || (!Directory.Exists(Path.Combine(path, ".git")) && !File.Exists(Path.Combine(path, ".git")))) return false;
        try
        {
            var origin = await RunGitAsync(path, ["remote", "get-url", "origin"], cancellationToken);
            return string.Equals(remotePolicy.GetRepositoryFullName(origin.StandardOutput.Trim()), expectedFullName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException) { return false; }
    }

    private async Task<GitCommandResult> RunGitAsync(string path, IReadOnlyList<string> arguments, CancellationToken cancellationToken,
        bool network = false, bool isolate = false)
    {
        var result = await runner.RunAsync(path, arguments, GitTimeout, cancellationToken,
            disableHooks: true, useGitCredentialManager: network, isolateCheckoutConfiguration: isolate);
        if (result.ExitCode != 0) throw new InvalidOperationException("Gitセットアップ処理に失敗しました。");
        return result;
    }

    private static async Task<LayoutManifest> ReadLayoutAsync(CancellationToken cancellationToken)
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FUREQ.Workspace.Infrastructure.workspace-layout.v1.json")
            ?? throw new InvalidOperationException("Workspace layout JSONが見つかりません。");
        var manifest = await JsonSerializer.DeserializeAsync<LayoutManifest>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
            ?? throw new InvalidDataException("Workspace layout JSONを読み取れません。");
        if (manifest.SchemaVersion != 1 || manifest.Organization != "FUREQ-DEVELOP" || manifest.Repositories.Count != 14)
            throw new InvalidDataException("Workspace layout JSONのschemaまたは内容が正しくありません。");
        if (manifest.Repositories.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 14
            || manifest.Repositories.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 14)
            throw new InvalidDataException("Workspace layoutに重複があります。");
        return manifest;
    }

    private static HashSet<string> SelectRepositories(WorkspaceSetupMode mode, string role, IReadOnlyCollection<string>? custom)
    {
        string[] names = mode switch
        {
            WorkspaceSetupMode.Full => [".github", ".github-private", "DevelopmentDocs", "DevelopmentTools", "RenderingLibrary", "RenderingWorkspace", "GameEngine", "EngineEditor", "EngineHub", "EngineWorkspace", "Game-001", "Game-001-Code", "Game-001-Art", "Game-001-Design"],
            WorkspaceSetupMode.Rendering => ["RenderingWorkspace", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
            WorkspaceSetupMode.Engine => ["EngineWorkspace", "GameEngine", "EngineEditor", "EngineHub", "DevelopmentTools", "DevelopmentDocs", "RenderingLibrary"],
            WorkspaceSetupMode.Game => ["Game-001", "Game-001-Code", "Game-001-Art", "Game-001-Design", "GameEngine", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
            WorkspaceSetupMode.SharedServices => ["DevelopmentTools", "DevelopmentDocs", ".github", ".github-private"],
            WorkspaceSetupMode.Custom => custom?.ToArray() ?? [],
            WorkspaceSetupMode.RoleBased when role.Contains("engine", StringComparison.OrdinalIgnoreCase) => ["EngineWorkspace", "GameEngine", "EngineEditor", "EngineHub", "DevelopmentTools", "DevelopmentDocs", "RenderingLibrary"],
            WorkspaceSetupMode.RoleBased when role.Contains("render", StringComparison.OrdinalIgnoreCase) => ["RenderingWorkspace", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
            WorkspaceSetupMode.RoleBased when role.Contains("game", StringComparison.OrdinalIgnoreCase) => ["Game-001", "Game-001-Code", "Game-001-Art", "Game-001-Design", "GameEngine", "RenderingLibrary", "DevelopmentTools", "DevelopmentDocs"],
            WorkspaceSetupMode.RoleBased => ["DevelopmentTools", "DevelopmentDocs", ".github", ".github-private"],
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        var result = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        if (result.Count == 0) throw new ArgumentException("Custom modeでは1件以上のリポジトリを選択してください。", nameof(custom));
        return result;
    }

    private static HashSet<string> ExpandDependencies(HashSet<string> selected)
    {
        var pending = new Queue<string>(selected);
        while (pending.TryDequeue(out var name))
        {
            if (!Dependencies.TryGetValue(name, out var dependencies)) continue;
            foreach (var dependency in dependencies) if (selected.Add(dependency)) pending.Enqueue(dependency);
        }
        return selected;
    }

    private static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("layout pathは相対パスである必要があります。");
        if (path.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
            throw new InvalidDataException("layout pathが保存先から外れています。");
        return path;
    }

    private static string SafeCombine(string root, string relativePath)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var target = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(fullRoot, target);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new InvalidDataException("セットアップ先がワークスペース外です。");
        return target;
    }

    private static bool HasReparsePoint(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

    private static bool IsSafeBranchName(string branch) => !string.IsNullOrWhiteSpace(branch) && branch.Length <= 200
        && !branch.StartsWith('-') && !branch.Contains("..", StringComparison.Ordinal) && !branch.Contains('\\')
        && branch.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/');

    private static string SafeMessage(Exception exception) => exception is OperationCanceledException
        ? "キャンセルされました。"
        : exception.Message.Length <= 500 ? exception.Message : exception.Message[..500];

    private sealed record LayoutManifest(int SchemaVersion, string Organization, List<LayoutRepository> Repositories);
    private sealed record LayoutRepository(string Name, string Path);
}
