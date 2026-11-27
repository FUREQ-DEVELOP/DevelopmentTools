using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);

public interface IGitProcessRunner
{
    Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken = default, bool disableHooks = false,
        bool disableFsMonitor = false, bool useGitCredentialManager = false);
    Task<string?> GetGitHubCredentialUsernameAsync(string workingDirectory, CancellationToken cancellationToken = default);
}

public interface IGitRemotePolicy
{
    string GetRepositoryFullName(string remoteUrl);
}

public interface IGitWriteIdentityValidator
{
    Task ValidateAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

public interface IGitRepositoryOperations
{
    Task<GitRepositorySnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task CreateBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default);
    Task<GitRepositorySnapshot> FetchAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<GitRepositorySnapshot> PullFastForwardAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<string> CommitStagedChangesAsync(string repositoryPath, string message, CancellationToken cancellationToken = default);
    Task PushAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

public interface IGitHubPullRequestService
{
    Task<PullRequestLink> CreateAsync(string repositoryPath, string title, string body, string baseBranch,
        CancellationToken cancellationToken = default);
}
