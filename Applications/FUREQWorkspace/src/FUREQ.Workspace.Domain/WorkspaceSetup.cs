namespace FUREQ.Workspace.Domain;

public enum WorkspaceSetupMode { Full, Rendering, Engine, Game, SharedServices, RoleBased, Custom }
public enum LfsDownloadMode { Development, Lightweight, Complete, Custom }
public enum SetupItemState { Planned, Reuse, Blocked, InProgress, Completed, Failed, Cancelled }

public sealed record WorkspaceSetupItem(
    long RepositoryId,
    string FullName,
    string CloneUrl,
    string DefaultBranch,
    string RelativePath,
    string TargetPath,
    SetupItemState State,
    string? Message,
    IReadOnlyList<string> Dependencies);

public sealed record WorkspaceSetupPlan(
    Guid Id,
    Guid WorkspaceProfileId,
    string RootPath,
    WorkspaceSetupMode Mode,
    LfsDownloadMode LfsMode,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<WorkspaceSetupItem> Items)
{
    public IReadOnlyList<WorkspaceSetupItem> Blockers => Items.Where(item => item.State == SetupItemState.Blocked).ToArray();
    public IReadOnlyList<WorkspaceSetupItem> CloneItems => Items.Where(item => item.State is SetupItemState.Planned or SetupItemState.Failed or SetupItemState.Cancelled).ToArray();
}

public sealed record SetupItemProgress(Guid PlanId, string FullName, string TargetPath, SetupItemState State, string? Message, DateTimeOffset UpdatedAtUtc);
