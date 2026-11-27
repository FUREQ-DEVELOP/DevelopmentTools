using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed class GitSubmoduleService(IGitProcessRunner runner, IGitRemotePolicy remotePolicy) : ISubmoduleService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);
    private const int MaximumDepth = 12;
    private const int MaximumEntries = 500;

    public async Task<SubmoduleGraph> GetGraphAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var entries = new List<SubmoduleGraphEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await ScanRepositoryAsync(root.Path, root.FullName, "", 0, entries, visited, cancellationToken);
        return new SubmoduleGraph(entries);
    }

    public async Task<SubmoduleGraph> InitializeMissingRecursivelyAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var graph = await GetGraphAsync(root.Path, cancellationToken);
        var initializable = graph.InitializationPreview;
        if (initializable.Count == 0) return graph;

        var rootFilters = await HasLocalFiltersAsync(root.Path, cancellationToken);
        if (rootFilters) throw new InvalidOperationException("ローカルGit設定にfilterコマンドがあるため、安全のためSubmodule初期化を中止しました。");

        // Initialize one level at a time so each newly discovered nested .gitmodules is validated before any deeper clone.
        for (var depth = 0; depth < MaximumDepth; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            graph = await GetGraphAsync(root.Path, cancellationToken);
            var pending = graph.InitializationPreview;
            if (pending.Count == 0) return graph;
            var parents = pending.GroupBy(entry => entry.ParentRepositoryPath, StringComparer.OrdinalIgnoreCase);
            foreach (var group in parents)
            {
                var parentRelativePath = group.Key;
                var parentPath = parentRelativePath.Length == 0 ? root.Path : ResolveSafePath(root.Path, parentRelativePath);
                if (!Directory.Exists(parentPath) || IsReparsePath(root.Path, parentPath))
                    throw new InvalidOperationException("Submoduleの親フォルダーが存在しないか、リンクされています。");
                if (await HasLocalFiltersAsync(parentPath, cancellationToken))
                    throw new InvalidOperationException("Submoduleの親にGit filter設定があるため、安全のため初期化を中止しました。");

                var direct = group.ToArray();
                var childNames = await ReadSubmoduleConfigurationAsync(parentPath, cancellationToken);
                var localUrls = await ReadLocalSubmoduleUrlsAsync(parentPath, cancellationToken);
                foreach (var entry in direct)
                {
                    var leaf = GetLocalSubmodulePath(entry);
                    var matching = childNames.Where(item => string.Equals(item.Path.Replace('\\', '/'), leaf, StringComparison.Ordinal)
                        && string.Equals(item.Name, entry.SubmoduleName, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (matching.Length != 1)
                        throw new InvalidOperationException("Submoduleの設定とgitlinkを一意に対応付けできません。");
                    if (localUrls.TryGetValue(entry.SubmoduleName, out var localUrl)
                        && !string.Equals(remotePolicy.GetRepositoryFullName(ResolveSubmoduleUrl(
                            entry.ParentRepositoryFullName, localUrl)), entry.RepositoryFullName, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("ローカルSubmodule URLが検証済みの.gitmodules URLと異なります。");
                    var destination = ResolveSafePath(parentPath, leaf);
                    if (IsReparsePath(parentPath, destination))
                        throw new InvalidOperationException("Submoduleの保存先にリンクがあるため、ユーザーデータ保護のため中止しました。");
                    if (Directory.Exists(destination) && !IsGitWorktree(destination)
                        && Directory.EnumerateFileSystemEntries(destination).Any())
                        throw new InvalidOperationException("Submoduleの保存先に既存ファイルがあるため、上書きせず中止しました。");
                }

                // URL policy was checked while constructing the graph. Git checks out the gitlink's recorded SHA.
                foreach (var batch in direct.Select(GetLocalSubmodulePath).Chunk(20))
                {
                    var result = await runner.RunAsync(parentPath,
                        ["submodule", "update", "--init", "--jobs=3", "--", .. batch], CommandTimeout,
                        cancellationToken, disableHooks: true, useGitCredentialManager: true,
                        isolateCheckoutConfiguration: true);
                    if (result.ExitCode != 0) throw new InvalidOperationException("Submodule初期化に失敗しました。GitHubのアクセス権とネットワークを確認してください。");
                }
            }
        }

        throw new InvalidOperationException("Submoduleの階層が上限（12）を超えました。");
    }

    private async Task ScanRepositoryAsync(string repositoryPath, string repositoryFullName, string relativePrefix, int depth,
        List<SubmoduleGraphEntry> entries, HashSet<string> visited, CancellationToken cancellationToken)
    {
        if (depth > MaximumDepth) throw new InvalidOperationException("Submoduleの階層が上限（12）を超えました。");
        var identity = Path.GetFullPath(repositoryPath);
        if (!visited.Add(identity)) throw new InvalidOperationException("Submodule graphに循環参照または同一フォルダーの重複があります。");

        var tree = await RunCheckedAsync(repositoryPath, ["ls-tree", "-r", "-z", "--full-tree", "HEAD"], cancellationToken);
        var links = ParseGitlinks(tree.StandardOutput);
        var configuration = await ReadSubmoduleConfigurationAsync(repositoryPath, cancellationToken);
        if (configuration.Count == 0)
        {
            if (links.Count != 0) throw new InvalidDataException("Git treeにgitlinkがありますが、.gitmodulesがありません。");
            return;
        }
        var pathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configured in configuration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subPath = ValidateRelativePath(configured.Path);
            if (!pathSet.Add(subPath)) throw new InvalidDataException(".gitmodules に重複したSubmoduleパスがあります。");
            if (!links.TryGetValue(subPath, out var pinnedCommit))
                throw new InvalidDataException(".gitmodulesのパスに対応するgitlinkがHEADにありません。");
            var absolutePath = ResolveSafePath(repositoryPath, subPath);
            var fullRelativePath = relativePrefix.Length == 0 ? subPath : $"{relativePrefix}/{subPath}";
            var resolvedUrl = ResolveSubmoduleUrl(repositoryFullName, configured.Url);
            var childFullName = remotePolicy.GetRepositoryFullName(resolvedUrl);
            if (entries.Count >= MaximumEntries) throw new InvalidDataException("Submodule数が上限（500）を超えました。");

            if (IsReparsePath(repositoryPath, absolutePath))
                throw new InvalidOperationException("Submoduleパスにリンクがあるため、安全のため走査を中止しました。");
            var isInitialized = IsGitWorktree(absolutePath);
            string? currentCommit = null;
            if (isInitialized)
            {
                var remote = await RunCheckedAsync(absolutePath, ["remote", "get-url", "origin"], cancellationToken);
                if (!string.Equals(remotePolicy.GetRepositoryFullName(remote.StandardOutput.Trim()), childFullName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Submoduleのoriginが.gitmodulesのURLと一致しません。");
                var current = await RunCheckedAsync(absolutePath, ["rev-parse", "HEAD"], cancellationToken);
                currentCommit = current.StandardOutput.Trim();
            }

            entries.Add(new SubmoduleGraphEntry(fullRelativePath, childFullName, pinnedCommit, currentCommit, isInitialized,
                relativePrefix, configured.Name, repositoryFullName));
            if (isInitialized)
                await ScanRepositoryAsync(absolutePath, childFullName, fullRelativePath, depth + 1, entries, visited, cancellationToken);
        }
        if (links.Keys.Any(linkPath => !pathSet.Contains(linkPath)))
            throw new InvalidDataException(".gitmodulesに対応する設定がないgitlinkがあります。");
    }

    private async Task<ResolvedRoot> ResolveRootAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        if (!Directory.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("選択したフォルダーがありません、またはリンクされています。");
        var top = await RunCheckedAsync(fullPath, ["rev-parse", "--show-toplevel"], cancellationToken);
        if (!string.Equals(fullPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(top.StandardOutput.Trim())), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("選択したフォルダーはGitリポジトリのルートではありません。");
        var remote = await RunCheckedAsync(fullPath, ["remote", "get-url", "origin"], cancellationToken);
        return new ResolvedRoot(fullPath, remotePolicy.GetRepositoryFullName(remote.StandardOutput.Trim()));
    }

    private async Task<IReadOnlyList<ConfiguredSubmodule>> ReadSubmoduleConfigurationAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(repositoryPath, ".gitmodules");
        if (!File.Exists(path)) return [];
        var tracked = await RunCheckedAsync(repositoryPath, ["ls-tree", "HEAD", "--", ".gitmodules"], cancellationToken);
        if (!tracked.StandardOutput.StartsWith("100644 blob ", StringComparison.Ordinal)
            && !tracked.StandardOutput.StartsWith("100755 blob ", StringComparison.Ordinal))
            throw new InvalidOperationException(".gitmodulesがHEADで通常ファイルとして管理されていません。");
        var diff = await runner.RunAsync(repositoryPath, ["diff", "--quiet", "HEAD", "--", ".gitmodules"], CommandTimeout,
            cancellationToken, disableHooks: true);
        if (diff.ExitCode == 1) throw new InvalidOperationException(".gitmodulesに未コミットまたはステージ済みの変更があるため、Submodule操作を中止しました。");
        if (diff.ExitCode != 0) throw new InvalidOperationException(".gitmodulesの状態を検証できませんでした。");

        var result = await RunCheckedAsync(repositoryPath,
            ["config", "--file", ".gitmodules", "--list", "--null"], cancellationToken);
        var sections = new Dictionary<string, MutableSubmodule>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('\n');
            if (separator <= 0) throw new InvalidDataException(".gitmodulesの設定形式が不正です。");
            var key = item[..separator];
            var value = item[(separator + 1)..];
            const string prefix = "submodule.";
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var suffixIndex = key.LastIndexOf('.');
            if (suffixIndex <= prefix.Length) continue;
            var property = key[(suffixIndex + 1)..];
            if (property is not ("path" or "url")) continue;
            var section = key[prefix.Length..suffixIndex];
            if (!sections.TryGetValue(section, out var data)) sections.Add(section, data = new MutableSubmodule());
            if (property == "path") data.Path = value;
            if (property == "url") data.Url = value;
        }

        var configured = sections.Values.Select(item =>
        {
            if (string.IsNullOrWhiteSpace(item.Path) || string.IsNullOrWhiteSpace(item.Url))
                throw new InvalidDataException(".gitmodulesにはpathとurlの両方が必要です。");
            return new ConfiguredSubmodule(sections.First(pair => ReferenceEquals(pair.Value, item)).Key, item.Path, item.Url);
        }).ToArray();
        return configured;
    }

    private async Task<Dictionary<string, string>> ReadLocalSubmoduleUrlsAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(repositoryPath,
            ["config", "--local", "--get-regexp", "--null", "^submodule\\..*\\.url$"], CommandTimeout,
            cancellationToken, disableHooks: true);
        if (result.ExitCode == 1) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (result.ExitCode != 0) throw new InvalidOperationException("ローカルSubmodule設定を検証できませんでした。");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('\n');
            if (separator <= 0) throw new InvalidDataException("ローカルSubmodule URL設定の形式が不正です。");
            var key = item[..separator];
            var name = key["submodule.".Length..^".url".Length];
            if (!values.TryAdd(name, item[(separator + 1)..]))
                throw new InvalidDataException("ローカルSubmodule URL設定が重複しています。");
        }
        return values;
    }

    private async Task<bool> HasLocalFiltersAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(repositoryPath,
            ["config", "--local", "--get-regexp", "^filter\\..*\\.(smudge|process|clean)$"], CommandTimeout,
            cancellationToken, disableHooks: true);
        if (result.ExitCode == 1) return false;
        if (result.ExitCode != 0) throw new InvalidOperationException("ローカルGit設定を検証できませんでした。");
        return !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private async Task<GitCommandResult> RunCheckedAsync(string path, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(path, arguments, CommandTimeout, cancellationToken, disableHooks: true);
        if (result.ExitCode != 0) throw new InvalidOperationException("Submodule情報をGitから読み取れませんでした。");
        return result;
    }

    private static Dictionary<string, string> ParseGitlinks(string output)
    {
        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf('\t');
            if (tab < 0) throw new InvalidDataException("Git treeの形式が不正です。");
            var metadata = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (metadata.Length != 3 || metadata[0] != "160000" || metadata[1] != "commit") continue;
            if (metadata[2].Length is not (40 or 64) || !metadata[2].All(Uri.IsHexDigit))
                throw new InvalidDataException("gitlinkに不正なcommit SHAがあります。");
            var path = ValidateRelativePath(record[(tab + 1)..]);
            if (!links.TryAdd(path, metadata[2])) throw new InvalidDataException("gitlinkパスが重複しています。");
        }
        return links;
    }

    private static string ResolveSubmoduleUrl(string parentFullName, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)) return absolute.AbsoluteUri;
        if (url.StartsWith("../", StringComparison.Ordinal) || url.StartsWith("./", StringComparison.Ordinal))
        {
            var parentRepo = parentFullName[("FUREQ-DEVELOP/".Length)..];
            var parentUri = new Uri($"https://github.com/FUREQ-DEVELOP/{Uri.EscapeDataString(parentRepo)}.git");
            return new Uri(parentUri, url).AbsoluteUri;
        }
        throw new InvalidOperationException("Submodule URLは許可されたHTTPS GitHub URLまたは相対URLにしてください。");
    }

    private static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Submodule pathが相対パスではありません。");
        var parts = path.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".."))
            throw new InvalidDataException("Submodule pathがワークスペース外を指しています。");
        return string.Join('/', parts);
    }

    private static string ResolveSafePath(string parent, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(parent, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), fullPath);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Submodule pathが親リポジトリ外を指しています。");
        return fullPath;
    }

    private static bool IsGitWorktree(string path) => Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));

    private static bool IsReparsePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
        }
        return false;
    }

    private static string GetLocalSubmodulePath(SubmoduleGraphEntry entry)
    {
        if (entry.ParentRepositoryPath.Length == 0) return entry.RelativePath;
        var prefix = entry.ParentRepositoryPath + "/";
        if (!entry.RelativePath.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Submodule pathが親リポジトリの相対パスと一致しません。");
        return entry.RelativePath[prefix.Length..];
    }

    private sealed record ResolvedRoot(string Path, string FullName);
    private sealed record ConfiguredSubmodule(string Name, string Path, string Url);
    private sealed class MutableSubmodule { public string? Path { get; set; } public string? Url { get; set; } }
}
