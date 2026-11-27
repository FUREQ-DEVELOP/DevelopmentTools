using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IWorkspaceAssetService
{
    Task<IReadOnlyList<WorkspaceAsset>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
