namespace FUREQ.Workspace.Domain;

public sealed record DeclaredDependency(string RepositoryFullName, string DependencyType, string? SdkVersion,
    string? ApiCompatibility, string? ReleaseUrl);

public sealed record DependencyGraphSnapshot(SubmoduleGraph Submodules, IReadOnlyList<DeclaredDependency> DeclaredDependencies);
