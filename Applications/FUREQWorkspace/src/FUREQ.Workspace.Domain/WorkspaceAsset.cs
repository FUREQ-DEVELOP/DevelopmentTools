namespace FUREQ.Workspace.Domain;

public sealed record WorkspaceAsset(string RelativePath, string FullPath, string Extension, long Length,
    DateTimeOffset LastModifiedUtc, bool IsImage, bool IsGitLfsPointer, string? LfsObjectId, long? LfsObjectSize);
