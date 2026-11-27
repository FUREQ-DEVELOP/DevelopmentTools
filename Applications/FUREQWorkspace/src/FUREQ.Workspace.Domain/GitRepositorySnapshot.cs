namespace FUREQ.Workspace.Domain;

public sealed record GitRepositorySnapshot(
    string RepositoryFullName,
    string Branch,
    string? Upstream,
    int Ahead,
    int Behind,
    bool IsDirty,
    bool HasConflicts,
    IReadOnlyList<string> ChangedPaths);

public sealed record PullRequestLink(string HtmlUrl, int Number, string Title);
