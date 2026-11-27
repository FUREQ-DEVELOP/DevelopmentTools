using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitHubPublicRepositoryDiscoveryTests
{
    [Fact]
    public async Task LoadsPublicRepositoriesAcrossPages()
    {
        var handler = new PagingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FUREQ-Workspace-tests/0.1");
        var discovery = new GitHubPublicRepositoryDiscovery(client);

        var repositories = await discovery.GetRepositoriesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(101, repositories.Count);
        Assert.Equal(42L, repositories[0].Id);
        Assert.Equal("FUREQ-DEVELOP/DevelopmentTools", repositories[0].FullName);
        Assert.Equal(2, handler.RequestCount);
        Assert.All(handler.Requests, request => Assert.Contains("type=public", request));
    }

    [Fact]
    public async Task Authenticated_discovery_uses_bearer_header_and_requests_only_app_visible_repositories()
    {
        var handler = new AuthenticatedHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FUREQ-Workspace-tests/0.1");
        var discovery = new GitHubPublicRepositoryDiscovery(client);

        await discovery.GetRepositoriesAsync("ghu-test-token", TestContext.Current.CancellationToken);

        Assert.Equal("Bearer ghu-test-token", handler.Authorization);
        Assert.Contains("type=all", handler.RequestUri);
        Assert.DoesNotContain("ghu-test-token", handler.RequestUri);
    }

    private sealed class PagingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Requests.Add(request.RequestUri!.PathAndQuery);
            var body = RequestCount == 1
                ? JsonSerializer.Serialize(Enumerable.Range(0, 100).Select(index => CreateRepository(index == 0 ? 42 : 1000 + index,
                    index == 0 ? "DevelopmentTools" : $"Repository{index}")))
                : JsonSerializer.Serialize(new[] { CreateRepository(43, "Workspace") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        private static object CreateRepository(long id, string name) => new
        {
            id,
            name,
            full_name = $"FUREQ-DEVELOP/{name}",
            html_url = $"https://github.com/FUREQ-DEVELOP/{name}",
            clone_url = $"https://github.com/FUREQ-DEVELOP/{name}.git",
            default_branch = "main",
            archived = false,
            fork = false,
            updated_at = "2026-01-01T00:00:00Z"
        };
    }

    private sealed class AuthenticatedHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
