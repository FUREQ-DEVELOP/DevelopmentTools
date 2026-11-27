using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface ISetupProgressStore
{
    Task SavePlanAsync(WorkspaceSetupPlan plan, CancellationToken cancellationToken = default);
    Task<WorkspaceSetupPlan?> GetLatestPlanAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task SaveAsync(SetupItemProgress progress, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SetupItemProgress>> GetAsync(Guid planId, CancellationToken cancellationToken = default);
}

public interface IWorkspaceSetupService
{
    Task<WorkspaceSetupPlan> CreatePlanAsync(WorkspaceProfile profile, WorkspaceSetupMode mode,
        IReadOnlyList<RepositoryMetadata> repositories, LfsDownloadMode lfsMode,
        IReadOnlyCollection<string>? customRepositories = null, CancellationToken cancellationToken = default);
    Task<WorkspaceSetupPlan> ExecuteAsync(WorkspaceSetupPlan plan, IProgress<SetupItemProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<WorkspaceSetupPlan?> GetLatestPlanAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SetupItemProgress>> GetProgressAsync(Guid planId, CancellationToken cancellationToken = default);
}
