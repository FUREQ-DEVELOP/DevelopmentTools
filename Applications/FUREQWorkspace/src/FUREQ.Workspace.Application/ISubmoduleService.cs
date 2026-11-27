using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface ISubmoduleService
{
    Task<SubmoduleGraph> GetGraphAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<SubmoduleGraph> InitializeMissingRecursivelyAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
