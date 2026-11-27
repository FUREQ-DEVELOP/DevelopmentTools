namespace FUREQ.Workspace.Domain;

public sealed record GitHubWorkItem(int Number, string NodeId, string Title, string State, string Url, string? Body,
    string Author, DateTimeOffset UpdatedAt, bool IsPullRequest);

public sealed record GitHubProject(string NodeId, string Title, string Url, bool Closed);

public sealed record GitHubProjectItem(string NodeId, string Title, string ContentType, string? Url, string? State);
