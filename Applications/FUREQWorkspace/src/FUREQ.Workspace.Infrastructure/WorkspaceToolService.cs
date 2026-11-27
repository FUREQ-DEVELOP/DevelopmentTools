using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed partial class WorkspaceToolService : IWorkspaceToolService
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumScannedDirectories = 5000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public async Task<IReadOnlyList<WorkspaceTool>> DiscoverAsync(string developmentToolsRoot, CancellationToken cancellationToken = default)
    {
        var root = GetRoot(developmentToolsRoot);
        var toolsDirectory = Path.Combine(root, "Tools");
        if (!Directory.Exists(toolsDirectory) || File.GetAttributes(toolsDirectory).HasFlag(FileAttributes.ReparsePoint)) return [];
        var manifests = new List<WorkspaceTool>();
        var directories = new Stack<string>();
        directories.Push(toolsDirectory);
        var visitedDirectories = 0;
        while (directories.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++visitedDirectories > MaximumScannedDirectories) throw new InvalidDataException("Manifest走査のディレクトリ上限に達しました。");
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint)) directories.Push(child);
            }
            foreach (var path in Directory.EnumerateFiles(directory, "*.fureq-tool.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) continue;
                var info = new FileInfo(path);
                if (info.Length > MaximumManifestBytes) continue;
                try
                {
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var manifest = await JsonSerializer.DeserializeAsync<ToolManifest>(stream, JsonOptions, cancellationToken);
                    if (manifest is not null && TryValidate(root, manifest, out var tool))
                    {
                        manifests.Add(tool);
                        if (manifests.Count > 1000) throw new InvalidDataException("有効なツールManifestが上限（1,000件）を超えています。");
                    }
                }
                catch (JsonException) { }
            }
        }
        return manifests.OrderBy(tool => tool.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<WorkspaceToolRunResult> RunAsync(string developmentToolsRoot, WorkspaceTool tool, CancellationToken cancellationToken = default)
    {
        var root = GetRoot(developmentToolsRoot);
        var candidate = Path.GetFullPath(tool.EntryPoint);
        if (!IsSafeWithin(root, candidate) || !File.Exists(candidate) || !candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("実行ファイルが許可されたDevelopmentTools checkout内にありません。");
        if (File.GetAttributes(candidate).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("リンクされた実行ファイルは起動できません。");

        var start = new ProcessStartInfo(candidate) { WorkingDirectory = Path.GetDirectoryName(candidate)!, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "windir", "PATH", "TEMP", "TMP" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value)) start.Environment[name] = value;
        }
        foreach (var argument in tool.Arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        var watch = Stopwatch.StartNew();
        if (!process.Start()) throw new InvalidOperationException("ツールを起動できませんでした。");
        using var drainCancellation = new CancellationTokenSource();
        var stdout = DrainAsync(process.StandardOutput, drainCancellation.Token);
        var stderr = DrainAsync(process.StandardError, drainCancellation.Token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(tool.TimeoutSeconds));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            drainCancellation.Cancel();
            try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("共通ツールが制限時間を超えたため終了しました。");
        }
        drainCancellation.CancelAfter(TimeSpan.FromSeconds(2));
        try { await Task.WhenAll(stdout, stderr); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        cancellationToken.ThrowIfCancellationRequested();
        watch.Stop();
        return new WorkspaceToolRunResult(tool.ToolId, process.ExitCode, watch.Elapsed);
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) return;
            Array.Clear(buffer, 0, read);
        }
    }

    private static bool TryValidate(string root, ToolManifest manifest, out WorkspaceTool tool)
    {
        tool = default!;
        if (manifest.SchemaVersion != 1 || !ToolIdRegex().IsMatch(manifest.ToolId ?? "") ||
            string.IsNullOrWhiteSpace(manifest.DisplayName) || manifest.DisplayName.Length > 100 ||
            manifest.Description is null || manifest.Description.Length > 1000 || string.IsNullOrWhiteSpace(manifest.Version) ||
            !string.Equals(manifest.ExecutionType, "executable", StringComparison.Ordinal) ||
            manifest.TimeoutSeconds is < 1 or > 3600 || string.IsNullOrWhiteSpace(manifest.SideEffectClassification) ||
            manifest.SideEffectClassification.Length > 100 || manifest.Arguments is null || manifest.Arguments.Length > 32 ||
            manifest.Arguments.Any(argument => argument is null || argument.Length > 2048)) return false;
        string entry;
        try
        {
            entry = Path.GetFullPath(Path.Combine(root, manifest.EntryPoint ?? ""));
            if (!IsSafeWithin(root, entry) || !File.Exists(entry) || !entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint)) return false;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
        tool = new WorkspaceTool(manifest.ToolId!, manifest.DisplayName, manifest.Description, manifest.Version, entry,
            manifest.SideEffectClassification, manifest.TimeoutSeconds, manifest.Arguments);
        return true;
    }

    private static string GetRoot(string root)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(full) || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint) ||
            !Directory.Exists(Path.Combine(full, ".git")) && !File.Exists(Path.Combine(full, ".git")) ||
            !Directory.Exists(Path.Combine(full, "Applications", "FUREQWorkspace")))
            throw new InvalidOperationException("DevelopmentToolsのGit checkoutを選択してください。");
        return full;
    }

    private static bool IsSafeWithin(string root, string path)
    {
        if (!(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(root, path, StringComparison.OrdinalIgnoreCase))) return false;
        var current = root;
        foreach (var component in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint)) return false;
        }
        return true;
    }

    private sealed record ToolManifest(int SchemaVersion, string? ToolId, string? DisplayName, string? Description,
        string? Version, string? EntryPoint, string? ExecutionType, string? SideEffectClassification,
        int TimeoutSeconds, string[]? Arguments);

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolIdRegex();
}
