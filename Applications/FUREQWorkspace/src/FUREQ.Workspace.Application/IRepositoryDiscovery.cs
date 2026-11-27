using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IRepositoryDiscovery
{
    Task<IReadOnlyList<RepositoryMetadata>> GetRepositoriesAsync(string? accessToken = null, CancellationToken cancellationToken = default);
}
