using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IWorkspaceToolService
{
    Task<IReadOnlyList<WorkspaceTool>> DiscoverAsync(string developmentToolsRoot, CancellationToken cancellationToken = default);
    Task<WorkspaceToolRunResult> RunAsync(string developmentToolsRoot, WorkspaceTool tool, CancellationToken cancellationToken = default);
}
