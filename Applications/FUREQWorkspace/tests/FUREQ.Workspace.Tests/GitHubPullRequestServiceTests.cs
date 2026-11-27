using System.Net;
using System.Text;
using System.Text.Json;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitHubPullRequestServiceTests
{
    [Fact]
    public async Task Creates_PR_with_user_token_in_header_and_validated_branch_names()
    {
        var handler = new PullRequestHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var service = new GitHubPullRequestService(client,
            new FakeGitOperations(new GitRepositorySnapshot("FUREQ-DEVELOP/DevelopmentTools", "feature/phase-05", "origin/feature/phase-05", 0, 0, false, false, [])),
            new FakeAuthorization("ghu-test-token"));

        var result = await service.CreateAsync("C:\\repo", "Git integration", "Summary", "main", TestContext.Current.CancellationToken);

        Assert.Equal(51, result.Number);
        Assert.EndsWith("/repos/FUREQ-DEVELOP/DevelopmentTools/pulls", handler.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("Bearer ghu-test-token", handler.Authorization);
        Assert.DoesNotContain("ghu-test-token", handler.RequestUri.AbsoluteUri);
        Assert.Equal("feature/phase-05", handler.Payload!.RootElement.GetProperty("head").GetString());
        Assert.Equal("main", handler.Payload.RootElement.GetProperty("base").GetString());
    }

    [Fact]
    public async Task Reports_unknown_write_outcome_on_timeout_without_retrying()
    {
        var handler = new PullRequestHandler(throwTimeout: true);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var service = new GitHubPullRequestService(client,
            new FakeGitOperations(new GitRepositorySnapshot("FUREQ-DEVELOP/DevelopmentTools", "feature/phase-05", "origin/feature/phase-05", 0, 0, false, false, [])),
            new FakeAuthorization("ghu-test-token"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("C:\\repo", "Git integration", "Summary", "main", TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class PullRequestHandler(bool throwTimeout = false) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public JsonDocument? Payload { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            if (throwTimeout) throw new TaskCanceledException("timed out");
            Payload = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"html_url\":\"https://github.com/FUREQ-DEVELOP/DevelopmentTools/pull/51\",\"number\":51,\"title\":\"Git integration\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FakeGitOperations(GitRepositorySnapshot snapshot) : IGitRepositoryOperations
    {
        public Task<GitRepositorySnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task<IReadOnlyList<string>> GetBranchesAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task CreateBranchAsync(string repositoryPath, string branchName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<GitRepositorySnapshot> FetchAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task<GitRepositorySnapshot> PullFastForwardAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task<string> CommitStagedChangesAsync(string repositoryPath, string message, CancellationToken cancellationToken = default) => Task.FromResult("commit");
        public Task PushAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAuthorization(string? token) : IGitHubAuthorization
    {
        public Task<DeviceAuthorizationRequest> BeginAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteAsync(DeviceAuthorizationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
        public void SignOut() => throw new NotSupportedException();
    }
}
