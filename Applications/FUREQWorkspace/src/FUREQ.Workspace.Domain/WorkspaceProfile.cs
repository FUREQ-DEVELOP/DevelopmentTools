namespace FUREQ.Workspace.Domain;

public sealed record WorkspaceProfile(Guid Id, string Name, string Role, string RootPath, DateTimeOffset CreatedAtUtc);

public sealed record LocalRepositoryLocation(
    Guid Id,
    Guid WorkspaceProfileId,
    string DirectoryName,
    string Path,
    bool IsPresent,
    DateTimeOffset LastScannedAtUtc);
