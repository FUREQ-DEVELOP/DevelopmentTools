using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IGitHubCollaborationService
{
    Task<IReadOnlyList<GitHubWorkItem>> GetIssuesAsync(string repositoryFullName, string state = "open", CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubWorkItem>> GetPullRequestsAsync(string repositoryFullName, string state = "open", CancellationToken cancellationToken = default);
    Task<GitHubWorkItem> CreateIssueAsync(string repositoryFullName, string title, string body,
        IReadOnlyList<string>? labels = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubProject>> GetOrganizationProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubProjectItem>> GetProjectItemsAsync(string projectNodeId, CancellationToken cancellationToken = default);
    Task AddIssueToProjectAsync(string projectNodeId, string issueNodeId, CancellationToken cancellationToken = default);
}
