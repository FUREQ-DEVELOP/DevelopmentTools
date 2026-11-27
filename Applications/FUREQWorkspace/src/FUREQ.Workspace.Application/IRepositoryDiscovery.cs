using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IRepositoryDiscovery
{
    Task<IReadOnlyList<RepositoryMetadata>> GetPublicRepositoriesAsync(CancellationToken cancellationToken = default);
}
