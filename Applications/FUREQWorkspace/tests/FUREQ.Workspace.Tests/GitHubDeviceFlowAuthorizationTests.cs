using System.Net;
using System.Text;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitHubDeviceFlowAuthorizationTests
{
    [Fact]
    public async Task Completes_device_flow_and_stores_tokens_in_vault()
    {
        var handler = new QueueHandler(
            "{\"device_code\":\"device-secret\",\"user_code\":\"ABCD-EFGH\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":600,\"interval\":1}",
            "{\"access_token\":\"ghu-access\",\"refresh_token\":\"ghr-refresh\",\"expires_in\":28800}");
        using var client = new HttpClient(handler);
        var vault = new MemoryCredentialVault();
        var authorization = new GitHubDeviceFlowAuthorization(client, vault);

        var deviceRequest = await authorization.BeginAsync(TestContext.Current.CancellationToken);
        await authorization.CompleteAsync(deviceRequest, TestContext.Current.CancellationToken);

        Assert.Equal("ghu-access", await authorization.GetAccessTokenAsync(TestContext.Current.CancellationToken));
        Assert.Contains("ghr-refresh", vault.Value);
        Assert.DoesNotContain("device-secret", handler.RequestUris);
        Assert.All(handler.RequestUris, uri => Assert.DoesNotContain("ghu-access", uri));
    }

    [Fact]
    public async Task Device_flow_honors_cancellation_while_waiting_to_poll()
    {
        var handler = new QueueHandler(
            "{\"device_code\":\"device-secret\",\"user_code\":\"ABCD-EFGH\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":600,\"interval\":30}");
        using var client = new HttpClient(handler);
        var authorization = new GitHubDeviceFlowAuthorization(client, new MemoryCredentialVault());
        var request = await authorization.BeginAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authorization.CompleteAsync(request, cancellation.Token));
        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task Slow_down_error_increases_poll_interval_by_five_seconds()
    {
        var handler = new QueueHandler(
            "{\"device_code\":\"device-secret\",\"user_code\":\"ABCD-EFGH\",\"verification_uri\":\"https://github.com/login/device\",\"expires_in\":600,\"interval\":1}",
            "{\"error\":\"slow_down\"}",
            "{\"access_token\":\"ghu-access\",\"refresh_token\":\"ghr-refresh\",\"expires_in\":28800}");
        using var client = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var authorization = new GitHubDeviceFlowAuthorization(client, new MemoryCredentialVault(), (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        var request = await authorization.BeginAsync(TestContext.Current.CancellationToken);

        await authorization.CompleteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 1d, 6d }, delays.Select(delay => delay.TotalSeconds));
        Assert.Equal(3, handler.RequestUris.Count);
    }

    [Fact]
    public async Task Refreshes_expired_access_token_using_saved_refresh_token()
    {
        var handler = new QueueHandler("{\"access_token\":\"ghu-renewed\",\"refresh_token\":\"ghr-rotated\",\"expires_in\":28800}");
        using var client = new HttpClient(handler);
        var vault = new MemoryCredentialVault(
            "{\"AccessToken\":\"ghu-expired\",\"RefreshToken\":\"ghr-old\",\"ExpiresAt\":\"2000-01-01T00:00:00+00:00\"}");
        var authorization = new GitHubDeviceFlowAuthorization(client, vault);

        var accessToken = await authorization.GetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ghu-renewed", accessToken);
        Assert.Contains("ghr-rotated", vault.Value);
        Assert.DoesNotContain("ghr-old", handler.RequestUris);
    }

    private sealed class MemoryCredentialVault : ICredentialVault
    {
        public MemoryCredentialVault(string? value = null) => Value = value;
        public string? Value { get; private set; }
        public string? Read(string target) => Value;
        public void Write(string target, string value) => Value = value;
        public void Delete(string target) => Value = null;
    }

    private sealed class QueueHandler(params string[] bodies) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(bodies);
        public List<string> RequestUris { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!.AbsoluteUri);
            if (request.Content is not null)
            {
                var content = await request.Content.ReadAsStringAsync(cancellationToken);
                Assert.DoesNotContain("device-secret", request.RequestUri.AbsoluteUri);
                Assert.DoesNotContain("ghu-access", content);
            }

            var body = _responses.Dequeue();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
