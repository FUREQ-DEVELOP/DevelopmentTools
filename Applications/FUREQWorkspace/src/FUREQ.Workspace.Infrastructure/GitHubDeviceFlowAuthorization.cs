using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FUREQ.Workspace.Application;

namespace FUREQ.Workspace.Infrastructure;

public sealed class GitHubDeviceFlowAuthorization : IGitHubAuthorization
{
    private readonly HttpClient _httpClient;
    private readonly ICredentialVault _credentialVault;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private const string ClientId = "Iv23liYwLxSj0gFZ6aVf";
    private const string CredentialTarget = "FUREQ.Workspace.GitHub.UserTokens";
    private const string DeviceGrant = "urn:ietf:params:oauth:grant-type:device_code";

    public GitHubDeviceFlowAuthorization(HttpClient httpClient, ICredentialVault credentialVault)
        : this(httpClient, credentialVault, Task.Delay)
    {
    }

    public GitHubDeviceFlowAuthorization(HttpClient httpClient, ICredentialVault credentialVault,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _httpClient = httpClient;
        _credentialVault = credentialVault;
        _delay = delay;
    }

    public async Task<DeviceAuthorizationRequest> BeginAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = "offline_access"
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("GitHub returned an invalid device authorization response.");
        if (result.VerificationUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(result.VerificationUri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || result.VerificationUri.AbsolutePath != "/login/device")
        {
            throw new InvalidDataException("GitHub returned an unexpected device verification address.");
        }
        return new DeviceAuthorizationRequest(result.DeviceCode, result.UserCode, result.VerificationUri,
            result.ExpiresIn, Math.Max(1, result.Interval));
    }

    public async Task CompleteAsync(DeviceAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(request.ExpiresIn);
        var interval = request.Interval;
        while (DateTimeOffset.UtcNow < expiresAt)
        {
            await _delay(TimeSpan.FromSeconds(interval), cancellationToken);
            using var response = await PostTokenAsync(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["device_code"] = request.DeviceCode,
                ["grant_type"] = DeviceGrant
            }, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("GitHub returned an invalid token response.");

            if (!string.IsNullOrEmpty(result.AccessToken))
            {
                SaveTokens(result);
                return;
            }

            switch (result.Error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += 5;
                    continue;
                case "access_denied":
                    throw new InvalidOperationException("GitHub authorization was cancelled.");
                case "expired_token":
                    throw new TimeoutException("The GitHub device code expired. Start sign-in again.");
                default:
                    throw new InvalidOperationException("GitHub could not authorize this account. Start sign-in again.");
            }
        }

        throw new TimeoutException("The GitHub device code expired. Start sign-in again.");
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var saved = ReadTokens();
        if (saved is null) return null;
        if (saved.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return saved.AccessToken;
        if (string.IsNullOrEmpty(saved.RefreshToken))
        {
            _credentialVault.Delete(CredentialTarget);
            return null;
        }

        using var response = await PostTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = saved.RefreshToken
        }, cancellationToken);
        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("GitHub returned an invalid refresh response.");
        if (string.IsNullOrEmpty(refreshed.AccessToken))
        {
            _credentialVault.Delete(CredentialTarget);
            return null;
        }

        SaveTokens(refreshed);
        return refreshed.AccessToken;
    }

    public void SignOut() => _credentialVault.Delete(CredentialTarget);

    private Task<HttpResponseMessage> PostTokenAsync(Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(values)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return _httpClient.SendAsync(request, cancellationToken);
    }

    private StoredTokens? ReadTokens()
    {
        var serialized = _credentialVault.Read(CredentialTarget);
        if (serialized is null) return null;
        try { return JsonSerializer.Deserialize<StoredTokens>(serialized); }
        catch (JsonException)
        {
            _credentialVault.Delete(CredentialTarget);
            return null;
        }
    }

    private void SaveTokens(TokenResponse response)
    {
        var tokens = new StoredTokens(response.AccessToken!, response.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn ?? 28800));
        _credentialVault.Write(CredentialTarget, JsonSerializer.Serialize(tokens));
    }

    private sealed record DeviceCodeResponse(
        [property: JsonPropertyName("device_code")] string DeviceCode,
        [property: JsonPropertyName("user_code")] string UserCode,
        [property: JsonPropertyName("verification_uri")] Uri VerificationUri,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("interval")] int Interval);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("refresh_token_expires_in")] int? RefreshTokenExpiresIn,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record StoredTokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
}

public sealed class GitHubAccountDiscovery(HttpClient httpClient) : IGitHubAccountDiscovery
{
    public async Task<GitHubAccount> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest("user", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("GitHub returned an invalid user response.");
        return new GitHubAccount(user.Login, user.Name ?? user.Login);
    }

    public async Task<IReadOnlyList<GitHubTeam>> GetTeamsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var teams = new List<GitHubTeam>();
        for (var page = 1; ; page++)
        {
            using var request = CreateRequest($"user/teams?per_page=100&page={page}", accessToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<TeamResponse[]>(cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("GitHub returned an invalid team list.");
            teams.AddRange(items
                .Where(team => string.Equals(team.Organization.Login, "FUREQ-DEVELOP", StringComparison.OrdinalIgnoreCase))
                .Select(team => new GitHubTeam(team.Name, team.Slug, team.HtmlUrl)));
            if (items.Length < 100) return teams;
        }
    }

    private static HttpRequestMessage CreateRequest(string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private sealed record UserResponse(
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record TeamResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("organization")] TeamOrganization Organization);

    private sealed record TeamOrganization([property: JsonPropertyName("login")] string Login);
}
