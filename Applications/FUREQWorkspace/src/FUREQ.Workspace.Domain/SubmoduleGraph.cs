namespace FUREQ.Workspace.Domain;

public sealed record SubmoduleGraphEntry(
    string RelativePath,
    string RepositoryFullName,
    string PinnedCommit,
    string? CurrentCommit,
    bool IsInitialized,
    string ParentRepositoryPath = "",
    string SubmoduleName = "",
    string ParentRepositoryFullName = "")
{
    public bool NeedsInitialization => !IsInitialized;
    public bool NeedsPinnedUpdate => IsInitialized && !string.Equals(CurrentCommit, PinnedCommit, StringComparison.OrdinalIgnoreCase);
}

public sealed record SubmoduleGraph(IReadOnlyList<SubmoduleGraphEntry> Entries)
{
    public IReadOnlyList<SubmoduleGraphEntry> InitializationPreview => Entries.Where(entry => entry.NeedsInitialization).ToArray();
    public IReadOnlyList<SubmoduleGraphEntry> PinnedUpdatePreview => Entries.Where(entry => entry.NeedsPinnedUpdate).ToArray();
}
