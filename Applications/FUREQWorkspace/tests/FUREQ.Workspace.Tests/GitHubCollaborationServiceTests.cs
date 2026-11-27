using System.Net;
using System.Text;
using System.Text.Json;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitHubCollaborationServiceTests
{
    [Fact]
    public async Task Lists_issues_without_pull_request_items_and_keeps_guest_reads_unauthenticated()
    {
        using var client = CreateSyncClient(_ => JsonResponse("""
            [
              {"number":7,"node_id":"I_7","title":"Bug","state":"open","html_url":"https://github.com/FUREQ-DEVELOP/Repo/issues/7","body":"body","user":{"login":"member"},"updated_at":"2026-11-27T00:00:00Z"},
              {"number":8,"node_id":"PR_8","title":"PR","state":"open","html_url":"https://github.com/FUREQ-DEVELOP/Repo/pull/8","body":null,"user":{"login":"member"},"updated_at":"2026-11-27T00:00:00Z","pull_request":{"url":"https://api.github.com/repos/FUREQ-DEVELOP/Repo/pulls/8"}}
            ]
            """));
        var service = new GitHubCollaborationService(client, new FakeAuthorization(null));

        var items = await service.GetIssuesAsync("FUREQ-DEVELOP/Repo", cancellationToken: TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Equal(7, item.Number);
        Assert.False(item.IsPullRequest);
    }

    [Fact]
    public async Task Creates_issue_with_user_token_in_header_and_no_production_write_in_test()
    {
        HttpRequestMessage? observedRequest = null;
        JsonDocument? payload = null;
        using var client = CreateClient(async request =>
        {
            observedRequest = request;
            payload = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            return JsonResponse("""
                {"number":51,"node_id":"I_51","title":"Test issue","state":"open","html_url":"https://github.com/FUREQ-DEVELOP/Repo/issues/51","body":"details","user":{"login":"fureq-dev"},"updated_at":"2026-11-27T00:00:00Z"}
                """, HttpStatusCode.Created);
        });
        var service = new GitHubCollaborationService(client, new FakeAuthorization("ghu-memory-token"));

        var result = await service.CreateIssueAsync("FUREQ-DEVELOP/Repo", " Test issue ", "details", ["bug"], TestContext.Current.CancellationToken);

        Assert.Equal(51, result.Number);
        Assert.Equal("Bearer ghu-memory-token", observedRequest!.Headers.Authorization!.ToString());
        Assert.DoesNotContain("ghu-memory-token", observedRequest.RequestUri!.AbsoluteUri);
        Assert.Equal("Test issue", payload!.RootElement.GetProperty("title").GetString());
        Assert.Equal("bug", payload.RootElement.GetProperty("labels")[0].GetString());
        payload.Dispose();
    }

    [Fact]
    public async Task Issue_write_timeout_is_unknown_and_not_retried()
    {
        var requests = 0;
        using var client = CreateSyncClient((HttpRequestMessage _) => { requests++; throw new TaskCanceledException("timeout"); });
        var service = new GitHubCollaborationService(client, new FakeAuthorization("ghu-memory-token"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateIssueAsync("FUREQ-DEVELOP/Repo", "title", "body", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task Lists_organization_projects_and_project_items_through_graphql()
    {
        var requestBodies = new List<string>();
        using var client = CreateClient(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            requestBodies.Add(body);
            return body.Contains("projectsV2(first", StringComparison.Ordinal)
                ? JsonResponse("""
                    {"data":{"organization":{"projectsV2":{"nodes":[{"id":"PVT_kw1","title":"Roadmap","url":"https://github.com/orgs/FUREQ-DEVELOP/projects/1","closed":false}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                    """)
                : JsonResponse("""
                    {"data":{"node":{"items":{"nodes":[{"id":"PVTI_1","content":{"__typename":"Issue","title":"Setup","url":"https://github.com/FUREQ-DEVELOP/Repo/issues/1","state":"OPEN"}},{"id":"PVTI_2","content":{"__typename":"DraftIssue","title":"Draft"}}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                    """);
        });
        var service = new GitHubCollaborationService(client, new FakeAuthorization("ghu-memory-token"));

        var projects = await service.GetOrganizationProjectsAsync(TestContext.Current.CancellationToken);
        var items = await service.GetProjectItemsAsync(Assert.Single(projects).NodeId, TestContext.Current.CancellationToken);

        Assert.Equal("Roadmap", projects[0].Title);
        Assert.Equal(2, items.Count);
        Assert.Equal("Issue", items[0].ContentType);
        Assert.Equal("DraftIssue", items[1].ContentType);
        Assert.Equal(2, requestBodies.Count);
        using var requestBody = JsonDocument.Parse(requestBodies[0]);
        Assert.True(requestBody.RootElement.TryGetProperty("query", out _));
        Assert.True(requestBody.RootElement.TryGetProperty("variables", out _));
    }

    [Fact]
    public async Task Adds_issue_to_project_with_graphql_mutation_and_user_token()
    {
        HttpRequestMessage? observedRequest = null;
        JsonDocument? payload = null;
        using var client = CreateClient(async request =>
        {
            observedRequest = request;
            payload = await JsonDocument.ParseAsync(await request.Content!.ReadAsStreamAsync(TestContext.Current.CancellationToken),
                cancellationToken: TestContext.Current.CancellationToken);
            return JsonResponse("""
                {"data":{"addProjectV2ItemById":{"item":{"id":"PVTI_added"}}}}
                """);
        });
        var service = new GitHubCollaborationService(client, new FakeAuthorization("ghu-memory-token"));

        await service.AddIssueToProjectAsync("PVT_project", "I_issue", TestContext.Current.CancellationToken);

        Assert.Equal("Bearer ghu-memory-token", observedRequest!.Headers.Authorization!.ToString());
        Assert.DoesNotContain("ghu-memory-token", observedRequest.RequestUri!.AbsoluteUri);
        Assert.Contains("addProjectV2ItemById", payload!.RootElement.GetProperty("query").GetString());
        Assert.Equal("PVT_project", payload.RootElement.GetProperty("variables").GetProperty("projectId").GetString());
        Assert.Equal("I_issue", payload.RootElement.GetProperty("variables").GetProperty("contentId").GetString());
        payload.Dispose();
    }

    [Theory]
    [InlineData("other-org/Repo")]
    [InlineData("FUREQ-DEVELOP/../Repo")]
    [InlineData("FUREQ-DEVELOP/Repo/extra")]
    public async Task Rejects_non_organization_or_malformed_repository_names(string repository)
    {
        using var client = CreateSyncClient((HttpRequestMessage _) => throw new Xunit.Sdk.XunitException("No request expected"));
        var service = new GitHubCollaborationService(client, new FakeAuthorization(null));
        await Assert.ThrowsAnyAsync<Exception>(() => service.GetIssuesAsync(repository, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static HttpClient CreateSyncClient(Func<HttpRequestMessage, HttpResponseMessage> send) =>
        new(new DelegateHandler(send)) { BaseAddress = new Uri("https://api.github.com/") };

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(new DelegateHandler(send)) { BaseAddress = new Uri("https://api.github.com/") };

    private static HttpResponseMessage JsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _asyncSend;
        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> send) => _asyncSend = (request, _) => Task.FromResult(send(request));
        public DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) => _asyncSend = (request, _) => send(request);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _asyncSend(request, cancellationToken);
    }

    private sealed class FakeAuthorization(string? token) : IGitHubAuthorization
    {
        public Task<DeviceAuthorizationRequest> BeginAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteAsync(DeviceAuthorizationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
        public void SignOut() => throw new NotSupportedException();
    }
}
