using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IWorkspaceInventory
{
    Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<WorkspaceProfile> CreateProfileAsync(string name, string role, string rootPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalRepositoryLocation>> GetRepositoriesAsync(Guid workspaceProfileId, CancellationToken cancellationToken = default);
    Task<int> ScanRepositoriesAsync(Guid workspaceProfileId, CancellationToken cancellationToken = default);
}
