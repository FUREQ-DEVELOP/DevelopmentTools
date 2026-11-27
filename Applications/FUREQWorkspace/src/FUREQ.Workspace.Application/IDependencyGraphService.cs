using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IDependencyGraphService
{
    Task<DependencyGraphSnapshot> LoadAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
