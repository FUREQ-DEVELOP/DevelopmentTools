using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed partial class DependencyGraphService(ISubmoduleService submoduleService) : IDependencyGraphService
{
    private const int MaximumManifestBytes = 64 * 1024;
    private const int MaximumDependencies = 500;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public async Task<DependencyGraphSnapshot> LoadAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var submodules = await submoduleService.GetGraphAsync(repositoryPath, cancellationToken);
        var manifestPath = Path.GetFullPath(Path.Combine(repositoryPath, ".fureq", "dependencies.json"));
        var declared = new List<DeclaredDependency>();
        var directory = Path.Combine(repositoryPath, ".fureq");
        if (Directory.Exists(directory) && File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("依存Manifestのフォルダーがリンクされているため読み込めません。");
        if (File.Exists(manifestPath))
        {
            if (File.GetAttributes(manifestPath).HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("依存Manifestはリンクできません。");
            var info = new FileInfo(manifestPath);
            if (info.Length > MaximumManifestBytes) throw new InvalidDataException("依存Manifestは64 KiB以内にしてください。");
            await using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var manifest = await JsonSerializer.DeserializeAsync<DependencyManifest>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("依存Manifestが空です。");
            if (manifest.SchemaVersion != 1 || manifest.Dependencies is null || manifest.Dependencies.Length > MaximumDependencies)
                throw new InvalidDataException("依存Manifestのschema versionまたは件数が不正です。");
            foreach (var item in manifest.Dependencies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item is null || !RepositoryRegex().IsMatch(item.Repository ?? "") ||
                    item.Type is not ("sdk" or "module" or "tool" or "api") ||
                    (item.SdkVersion?.Length ?? 0) > 100 || (item.ApiCompatibility?.Length ?? 0) > 200 ||
                    !IsAllowedReleaseUrl(item.ReleaseUrl)) throw new InvalidDataException("依存Manifestに不正なrepository、種別、version、またはRelease URLがあります。");
                declared.Add(new DeclaredDependency(item.Repository!, item.Type, item.SdkVersion, item.ApiCompatibility, item.ReleaseUrl));
            }
            if (declared.Select(item => item.RepositoryFullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != declared.Count)
                throw new InvalidDataException("依存Manifestに重複repositoryがあります。");
        }
        return new DependencyGraphSnapshot(submodules, declared);
    }

    private static bool IsAllowedReleaseUrl(string? value) => value is null ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        uri.AbsolutePath.StartsWith("/FUREQ-DEVELOP/", StringComparison.OrdinalIgnoreCase);

    private sealed record DependencyManifest(int SchemaVersion, DependencyItem[]? Dependencies);
    private sealed record DependencyItem(string? Repository, string? Type, string? SdkVersion, string? ApiCompatibility, string? ReleaseUrl);

    [GeneratedRegex("^FUREQ-DEVELOP/[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RepositoryRegex();
}
