using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed partial class WorkspaceAssetService : IWorkspaceAssetService
{
    private const int MaximumAssets = 20_000;
    private const long MaximumScannedBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".svg", ".ico" };
    private static readonly string[] SkippedDirectories = [".git", ".vs", "bin", "obj", "node_modules", "artifacts"];

    public async Task<IReadOnlyList<WorkspaceAsset>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryPath));
        if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("リポジトリがありません、またはリンクされています。");
        if (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
            throw new InvalidOperationException("選択先がGitリポジトリではありません。");

        var assets = new List<WorkspaceAsset>();
        var directories = new Stack<string>();
        directories.Push(root);
        long totalBytes = 0;
        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                directories.Push(child);
            }
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                var info = new FileInfo(path);
                totalBytes += info.Length;
                if (totalBytes > MaximumScannedBytes) throw new InvalidDataException("資産一覧の走査上限（2 GiB）に達しました。");
                var extension = info.Extension;
                var isImage = ImageExtensions.Contains(extension);
                if (!isImage && info.Length > 1024) continue;
                var sample = await ReadPointerSampleAsync(path, info.Length, cancellationToken);
                var pointer = ParsePointer(sample);
                if (!isImage && pointer.ObjectId is null) continue;
                if (assets.Count >= MaximumAssets) throw new InvalidDataException("資産数が上限（20,000）を超えました。");
                var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
                assets.Add(new WorkspaceAsset(relative, path, extension, info.Length, info.LastWriteTimeUtc,
                    isImage, pointer.ObjectId is not null, pointer.ObjectId, pointer.Size));
            }
        }
        return assets.OrderBy(asset => asset.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static async Task<string> ReadPointerSampleAsync(string path, long fileLength, CancellationToken cancellationToken)
    {
        if (fileLength is 0 or > 1024) return "";
        var buffer = new byte[(int)fileLength];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static readonly Regex LfsOidRegex = OidRegex();
    private static readonly Regex LfsSizeRegex = SizeRegex();

    private static (string? ObjectId, long? Size) ParsePointer(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!text.StartsWith("version https://git-lfs.github.com/spec/v1\n", StringComparison.Ordinal)) return (null, null);
        var oid = LfsOidRegex.Match(text);
        var size = LfsSizeRegex.Match(text);
        if (!oid.Success || !size.Success || !long.TryParse(size.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
            return (null, null);
        return (oid.Groups[1].Value, bytes);
    }

    [GeneratedRegex("(?m)^oid sha256:([0-9a-f]{64})$", RegexOptions.CultureInvariant)]
    private static partial Regex OidRegex();
    [GeneratedRegex("(?m)^size ([0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SizeRegex();
}
