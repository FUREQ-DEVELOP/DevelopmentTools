using System.Text.RegularExpressions;
using FUREQ.Workspace.Application;

namespace FUREQ.Workspace.Infrastructure;

public sealed partial class GitHubOrganizationRemotePolicy : IGitRemotePolicy
{
    public string GetRepositoryFullName(string remoteUrl)
    {
        if (!Uri.TryCreate(remoteUrl.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("originはFUREQ-DEVELOPのHTTPS GitHub URLにしてください。");
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 2 || !string.Equals(Uri.UnescapeDataString(segments[0]), "FUREQ-DEVELOP", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("originがFUREQ-DEVELOPのリポジトリではありません。");
        }

        var repositoryName = Uri.UnescapeDataString(segments[1]);
        if (repositoryName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repositoryName = repositoryName[..^4];
        if (!RepositoryNameRegex().IsMatch(repositoryName) || repositoryName is "." or "..")
        {
            throw new InvalidOperationException("originのリポジトリ名が正しくありません。");
        }

        return $"FUREQ-DEVELOP/{repositoryName}";
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryNameRegex();
}
