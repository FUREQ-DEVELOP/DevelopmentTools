namespace FUREQ.Workspace.Domain;

public sealed record RepositoryMetadata(
    long Id,
    string Name,
    string FullName,
    string? Description,
    string HtmlUrl,
    string CloneUrl,
    string DefaultBranch,
    bool Archived,
    bool Fork,
    DateTimeOffset UpdatedAt);
