using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitHubGitWriteIdentityValidatorTests
{
    [Fact]
    public async Task Rejects_push_when_GitHub_App_and_Git_Credential_Manager_accounts_differ()
    {
        var validator = CreateValidator("fureq-member", "other-account");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.ValidateAsync("C:\\repos\\demo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Allows_push_when_both_GitHub_accounts_match_case_insensitively()
    {
        var validator = CreateValidator("Fureq-Member", "fureq-member");

        await validator.ValidateAsync("C:\\repos\\demo", TestContext.Current.CancellationToken);
    }

    private static GitHubGitWriteIdentityValidator CreateValidator(string appLogin, string gitLogin) =>
        new(new FakeGitRunner(gitLogin), new FakeAuthorization(), new FakeAccountDiscovery(appLogin));

    private sealed class FakeAuthorization : IGitHubAuthorization
    {
        public Task<DeviceAuthorizationRequest> BeginAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteAsync(DeviceAuthorizationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("memory-only-test-token");
        public void SignOut() => throw new NotSupportedException();
    }

    private sealed class FakeAccountDiscovery(string login) : IGitHubAccountDiscovery
    {
        public Task<GitHubAccount> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GitHubAccount(login, login));
        public Task<IReadOnlyList<GitHubTeam>> GetTeamsAsync(string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GitHubTeam>>([]);
    }

    private sealed class FakeGitRunner(string username) : IGitProcessRunner
    {
        public Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken = default, bool disableHooks = false,
            bool disableFsMonitor = false, bool useGitCredentialManager = false) =>
            Task.FromResult(new GitCommandResult(0, "", ""));

        public Task<string?> GetGitHubCredentialUsernameAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(username);
    }
}
