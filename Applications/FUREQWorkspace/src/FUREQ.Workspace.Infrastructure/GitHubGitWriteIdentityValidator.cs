using FUREQ.Workspace.Application;

namespace FUREQ.Workspace.Infrastructure;

public sealed class GitHubGitWriteIdentityValidator(
    IGitProcessRunner processRunner,
    IGitHubAuthorization authorization,
    IGitHubAccountDiscovery accountDiscovery) : IGitWriteIdentityValidator
{
    public async Task ValidateAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var accessToken = await authorization.GetAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("Push前にFUREQ WorkspaceでGitHubへサインインしてください。");
        var account = await accountDiscovery.GetCurrentAccountAsync(accessToken, cancellationToken);
        var gitUsername = await processRunner.GetGitHubCredentialUsernameAsync(repositoryPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(gitUsername))
            throw new InvalidOperationException("Git Credential ManagerのGitHubアカウントを確認できません。先にGitHub Desktop等でサインインしてください。");
        if (!string.Equals(account.Login, gitUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"GitHub Appのアカウント (@{account.Login}) とGit Credential Manager (@{gitUsername}) が異なります。書き込みを中止しました。");
        }
    }
}
