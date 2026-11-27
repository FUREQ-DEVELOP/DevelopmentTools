using System.Collections.Concurrent;
using System.Text;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed class MarkdownDocumentService : IMarkdownDocumentService
{
    private const int MaximumFileBytes = 2 * 1024 * 1024;
    private const int MaximumSearchFiles = 5000;
    private const long MaximumTotalSearchBytes = 50 * 1024 * 1024;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _pathLocks = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["meeting"] = "# 会議名\n\n## 目的\n\n## 決定事項\n- \n\n## Action Items\n- [ ] 担当: 内容（期限: YYYY-MM-DD）\n",
        ["issue"] = "# 概要\n\n## 再現手順\n1. \n\n## 期待する動作\n\n## 実際の動作\n\n## 補足\n",
        ["design"] = "# 設計名\n\n## 目的\n\n## 背景\n\n## 決定\n\n## 代替案\n\n## 影響\n"
    };

    public async Task<IReadOnlyList<MarkdownDocument>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var root = ResolveRepositoryRoot(repositoryPath);
        var documents = new List<MarkdownDocument>();
        long totalBytes = 0;
        foreach (var file in EnumerateMarkdownFiles(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.Length > MaximumFileBytes) continue;
            totalBytes += info.Length;
            if (totalBytes > MaximumTotalSearchBytes || documents.Count >= MaximumSearchFiles)
                throw new InvalidDataException("Markdown一覧が上限を超えました。対象を絞ってください。");
            documents.Add(ToDocument(root, file, ""));
        }
        return documents.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<MarkdownDocument> ReadAsync(string repositoryPath, string relativePath, CancellationToken cancellationToken = default)
    {
        var root = ResolveRepositoryRoot(repositoryPath);
        var path = ResolveMarkdownPath(root, relativePath);
        EnsureNoReparsePath(root, path, allowMissingLeaf: false);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Markdownファイルがありません。", relativePath);
        if (info.Length > MaximumFileBytes) throw new InvalidDataException("Markdownファイルは2 MiB以下にしてください。");
        var content = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), cancellationToken);
        return ToDocument(root, path, content);
    }

    public async Task<IReadOnlyList<MarkdownSearchResult>> SearchAsync(string repositoryPath, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        if (query.Length > 200) throw new ArgumentException("検索語は200文字以内で入力してください。", nameof(query));
        var root = ResolveRepositoryRoot(repositoryPath);
        var results = new List<MarkdownSearchResult>();
        long totalBytes = 0;
        foreach (var file in EnumerateMarkdownFiles(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (info.Length > MaximumFileBytes) continue;
            totalBytes += info.Length;
            if (totalBytes > MaximumTotalSearchBytes || results.Count >= MaximumSearchFiles)
                throw new InvalidDataException("検索対象が上限を超えました。対象を絞ってください。");
            var lines = await File.ReadAllLinesAsync(file, new UTF8Encoding(false, true), cancellationToken);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                var preview = lines[index].Trim();
                if (preview.Length > 240) preview = preview[..240];
                results.Add(new MarkdownSearchResult(Path.GetRelativePath(root, file), index + 1, preview));
                if (results.Count >= MaximumSearchFiles) return results;
            }
        }
        return results;
    }

    public async Task SaveAsync(string repositoryPath, string relativePath, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Encoding.UTF8.GetByteCount(content) > MaximumFileBytes) throw new InvalidDataException("保存できるMarkdownは2 MiB以下です。");
        var root = ResolveRepositoryRoot(repositoryPath);
        var path = ResolveMarkdownPath(root, relativePath);
        EnsureNoReparsePath(root, path, allowMissingLeaf: true);
        var parent = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(parent);
        EnsureNoReparsePath(root, path, allowMissingLeaf: true);
        var gate = _pathLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = File.Exists(path) ? new FileInfo(path) : null;
            if (current is not null && current.Length > MaximumFileBytes) throw new InvalidDataException("既存Markdownは2 MiBを超えています。");
            var temporary = Path.Combine(parent, $".fureq-{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
                EnsureNoReparsePath(root, path, allowMissingLeaf: true);
                if (File.Exists(path)) File.Move(temporary, path, overwrite: true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        finally { gate.Release(); }
    }

    public string GetTemplate(string templateName) => Templates.TryGetValue(templateName, out var template)
        ? template
        : throw new ArgumentException("テンプレートを選択してください。", nameof(templateName));

    private static string ResolveRepositoryRoot(string repositoryPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("リポジトリフォルダーがありません、またはリンクされています。");
        if (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
            throw new InvalidOperationException("選択先がGitリポジトリではありません。");
        return root;
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SkipDirectoryNames.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                pending.Push(child);
            }
            foreach (var file in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                yield return file;
            }
        }
    }

    private static readonly string[] SkipDirectoryNames = [".git", ".vs", "bin", "obj", "node_modules", "artifacts"];

    private static string ResolveMarkdownPath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)
            || relativePath.Contains(':') || relativePath.Contains('\\') || !relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Markdownパスはリポジトリ内の相対 .md パスで指定してください。", nameof(relativePath));
        if (relativePath.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new ArgumentException("Markdownパスに親フォルダー参照は使えません。", nameof(relativePath));
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new ArgumentException("Markdownパスがリポジトリ外です。", nameof(relativePath));
        return path;
    }

    private static void EnsureNoReparsePath(string root, string path, bool allowMissingLeaf)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        var parts = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            current = Path.Combine(current, parts[index]);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                if (index == parts.Length - 1 && allowMissingLeaf) return;
                continue;
            }
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Markdownパスにリンクがあるため操作を中止しました。");
        }
    }

    private static MarkdownDocument ToDocument(string root, string path, string content)
    {
        var info = new FileInfo(path);
        return new MarkdownDocument(Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'), path,
            content, info.Exists ? info.Length : 0, info.Exists ? info.LastWriteTimeUtc : DateTimeOffset.UtcNow);
    }
}
