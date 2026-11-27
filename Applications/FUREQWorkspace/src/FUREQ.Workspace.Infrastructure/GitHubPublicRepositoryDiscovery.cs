using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed class GitHubPublicRepositoryDiscovery(HttpClient httpClient) : IRepositoryDiscovery
{
    private const string Organization = "FUREQ-DEVELOP";

    public async Task<IReadOnlyList<RepositoryMetadata>> GetPublicRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var repositories = new List<RepositoryMetadata>();
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"orgs/{Organization}/repos?type=public&sort=full_name&direction=asc&per_page=100&page={page}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<GitHubRepository[]>(cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("GitHub returned an invalid repository list.");

            repositories.AddRange(items.Select(item => new RepositoryMetadata(
                item.Id, item.Name, item.FullName, item.Description, item.HtmlUrl, item.CloneUrl,
                item.DefaultBranch, item.Archived, item.Fork, item.UpdatedAt)));

            if (items.Length < 100)
            {
                return repositories;
            }
        }
    }

    private sealed record GitHubRepository(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("full_name")] string FullName,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("clone_url")] string CloneUrl,
        [property: JsonPropertyName("default_branch")] string DefaultBranch,
        [property: JsonPropertyName("archived")] bool Archived,
        [property: JsonPropertyName("fork")] bool Fork,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
}
