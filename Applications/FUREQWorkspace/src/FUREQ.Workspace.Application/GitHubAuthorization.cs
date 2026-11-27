namespace FUREQ.Workspace.Application;

public sealed record DeviceAuthorizationRequest(string DeviceCode, string UserCode, Uri VerificationUri, int ExpiresIn, int Interval);
public sealed record GitHubAccount(string Login, string Name);
public sealed record GitHubTeam(string Name, string Slug, string HtmlUrl);

public interface ICredentialVault
{
    string? Read(string target);
    void Write(string target, string value);
    void Delete(string target);
}

public interface IGitHubAuthorization
{
    Task<DeviceAuthorizationRequest> BeginAsync(CancellationToken cancellationToken = default);
    Task CompleteAsync(DeviceAuthorizationRequest request, CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    void SignOut();
}

public interface IGitHubAccountDiscovery
{
    Task<GitHubAccount> GetCurrentAccountAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubTeam>> GetTeamsAsync(string accessToken, CancellationToken cancellationToken = default);
}
