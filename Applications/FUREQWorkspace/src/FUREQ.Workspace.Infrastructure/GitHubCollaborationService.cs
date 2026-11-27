using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed partial class GitHubCollaborationService(HttpClient httpClient, IGitHubAuthorization authorization)
    : IGitHubCollaborationService
{
    private const int PageSize = 100;
    private const int MaximumPages = 100;
    private const int MaximumResponseBytes = 4 * 1024 * 1024;

    public async Task<IReadOnlyList<GitHubWorkItem>> GetIssuesAsync(string repositoryFullName, string state = "open",
        CancellationToken cancellationToken = default)
    {
        var repository = ValidateRepository(repositoryFullName);
        ValidateState(state);
        var token = await authorization.GetAccessTokenAsync(cancellationToken);
        var results = new List<GitHubWorkItem>();
        for (var page = 1; page <= MaximumPages; page++)
        {
            using var request = CreateRestRequest(HttpMethod.Get,
                $"repos/FUREQ-DEVELOP/{repository}/issues?state={state}&per_page={PageSize}&page={page}", token);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var items = await ReadJsonAsync<IssueResponse[]>(response, cancellationToken)
                ?? throw new InvalidDataException("GitHubからIssue一覧を読み取れませんでした。");
            results.AddRange(items.Where(item => item.PullRequest is null).Select(MapWorkItem));
            if (items.Length < PageSize) return results;
        }
        throw new InvalidDataException("Issueのページ数が上限を超えました。");
    }

    public async Task<IReadOnlyList<GitHubWorkItem>> GetPullRequestsAsync(string repositoryFullName, string state = "open",
        CancellationToken cancellationToken = default)
    {
        var repository = ValidateRepository(repositoryFullName);
        ValidateState(state);
        var token = await authorization.GetAccessTokenAsync(cancellationToken);
        var results = new List<GitHubWorkItem>();
        for (var page = 1; page <= MaximumPages; page++)
        {
            using var request = CreateRestRequest(HttpMethod.Get,
                $"repos/FUREQ-DEVELOP/{repository}/pulls?state={state}&per_page={PageSize}&page={page}", token);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var items = await ReadJsonAsync<PullRequestResponse[]>(response, cancellationToken)
                ?? throw new InvalidDataException("GitHubからPull Request一覧を読み取れませんでした。");
            results.AddRange(items.Select(MapWorkItem));
            if (items.Length < PageSize) return results;
        }
        throw new InvalidDataException("Pull Requestのページ数が上限を超えました。");
    }

    public async Task<GitHubWorkItem> CreateIssueAsync(string repositoryFullName, string title, string body,
        IReadOnlyList<string>? labels = null, CancellationToken cancellationToken = default)
    {
        var repository = ValidateRepository(repositoryFullName);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256) throw new ArgumentException("Issueタイトルは1〜256文字で入力してください。", nameof(title));
        if (body.Length > 65536) throw new ArgumentException("Issue本文は65,536文字以内で入力してください。", nameof(body));
        if (labels is { Count: > 10 } || labels?.Any(label => string.IsNullOrWhiteSpace(label) || label.Length > 50) == true)
            throw new ArgumentException("ラベルは10件以内、各50文字以内で入力してください。", nameof(labels));
        var token = await authorization.GetAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("Issue作成にはGitHubでのサインインが必要です。");
        using var request = CreateRestRequest(HttpMethod.Post, $"repos/FUREQ-DEVELOP/{repository}/issues", token);
        request.Content = JsonContent.Create(new CreateIssueRequest(title.Trim(), body, labels ?? []));
        HttpResponseMessage response;
        try { response = await httpClient.SendAsync(request, cancellationToken); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Issue作成の結果を確認できませんでした。GitHub上で作成済みか確認してから再試行してください。");
        }
        using (response)
        {
            response.EnsureSuccessStatusCode();
            var item = await ReadJsonAsync<IssueResponse>(response, cancellationToken)
                ?? throw new InvalidDataException("GitHubから作成したIssue情報を読み取れませんでした。");
            return MapWorkItem(item);
        }
    }

    public async Task<IReadOnlyList<GitHubProject>> GetOrganizationProjectsAsync(CancellationToken cancellationToken = default)
    {
        var token = await authorization.GetAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("Organization Projectsの表示にはGitHubでのサインインが必要です。");
        const string query = """
            query($login: String!, $first: Int!, $after: String) {
              organization(login: $login) {
                projectsV2(first: $first, after: $after) {
                  nodes { id title url closed }
                  pageInfo { hasNextPage endCursor }
                }
              }
            }
            """;
        var projects = new List<GitHubProject>();
        string? cursor = null;
        for (var page = 0; page < MaximumPages; page++)
        {
            using var document = await SendGraphQlAsync(query, new { login = "FUREQ-DEVELOP", first = PageSize, after = cursor }, token, cancellationToken);
            var connection = document.RootElement.GetProperty("data").GetProperty("organization").GetProperty("projectsV2");
            foreach (var node in connection.GetProperty("nodes").EnumerateArray())
                projects.Add(new GitHubProject(node.GetProperty("id").GetString()!, node.GetProperty("title").GetString()!,
                    node.GetProperty("url").GetString()!, node.GetProperty("closed").GetBoolean()));
            var pageInfo = connection.GetProperty("pageInfo");
            if (!pageInfo.GetProperty("hasNextPage").GetBoolean()) return projects;
            var nextCursor = pageInfo.GetProperty("endCursor").GetString();
            if (string.IsNullOrEmpty(nextCursor) || nextCursor == cursor) throw new InvalidDataException("Projectsのページカーソルが不正です。");
            cursor = nextCursor;
        }
        throw new InvalidDataException("Projectsのページ数が上限を超えました。");
    }

    public async Task<IReadOnlyList<GitHubProjectItem>> GetProjectItemsAsync(string projectNodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectNodeId) || projectNodeId.Length > 256)
            throw new ArgumentException("Project IDが正しくありません。", nameof(projectNodeId));
        var token = await authorization.GetAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("Project項目の表示にはGitHubでのサインインが必要です。");
        const string query = """
            query($id: ID!, $first: Int!, $after: String) {
              node(id: $id) {
                ... on ProjectV2 {
                  items(first: $first, after: $after) {
                    nodes { id content {
                      __typename
                      ... on Issue { title url state }
                      ... on PullRequest { title url state }
                      ... on DraftIssue { title }
                    } }
                    pageInfo { hasNextPage endCursor }
                  }
                }
              }
            }
            """;
        var items = new List<GitHubProjectItem>();
        string? cursor = null;
        for (var page = 0; page < MaximumPages; page++)
        {
            using var document = await SendGraphQlAsync(query, new { id = projectNodeId, first = PageSize, after = cursor }, token, cancellationToken);
            var connection = document.RootElement.GetProperty("data").GetProperty("node").GetProperty("items");
            foreach (var node in connection.GetProperty("nodes").EnumerateArray())
            {
                var content = node.GetProperty("content");
                var contentType = content.GetProperty("__typename").GetString() ?? "Unknown";
                var title = contentType == "DraftIssue" || contentType == "Issue" || contentType == "PullRequest"
                    ? content.GetProperty("title").GetString() ?? "(no title)" : "(unknown item)";
                var url = content.TryGetProperty("url", out var urlNode) && urlNode.ValueKind == JsonValueKind.String ? urlNode.GetString() : null;
                var state = content.TryGetProperty("state", out var stateNode) && stateNode.ValueKind == JsonValueKind.String ? stateNode.GetString() : null;
                items.Add(new GitHubProjectItem(node.GetProperty("id").GetString()!, title, contentType, url, state));
            }
            var pageInfo = connection.GetProperty("pageInfo");
            if (!pageInfo.GetProperty("hasNextPage").GetBoolean()) return items;
            var nextCursor = pageInfo.GetProperty("endCursor").GetString();
            if (string.IsNullOrEmpty(nextCursor) || nextCursor == cursor) throw new InvalidDataException("Project項目のページカーソルが不正です。");
            cursor = nextCursor;
        }
        throw new InvalidDataException("Project項目のページ数が上限を超えました。");
    }

    public async Task AddIssueToProjectAsync(string projectNodeId, string issueNodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectNodeId) || projectNodeId.Length > 256
            || string.IsNullOrWhiteSpace(issueNodeId) || issueNodeId.Length > 256)
            throw new ArgumentException("ProjectまたはIssue IDが正しくありません。");
        var token = await authorization.GetAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("ProjectへのIssue追加にはGitHubでのサインインが必要です。");
        const string mutation = """
            mutation($projectId: ID!, $contentId: ID!) {
              addProjectV2ItemById(input: {projectId: $projectId, contentId: $contentId}) { item { id } }
            }
            """;
        using var document = await SendGraphQlAsync(mutation,
            new { projectId = projectNodeId, contentId = issueNodeId }, token, cancellationToken, isWrite: true);
        if (!document.RootElement.GetProperty("data").GetProperty("addProjectV2ItemById").TryGetProperty("item", out _))
            throw new InvalidDataException("IssueをProjectへ追加した結果を確認できませんでした。");
    }

    private async Task<JsonDocument> SendGraphQlAsync(string query, object variables, string token, CancellationToken cancellationToken, bool isWrite = false)
    {
        using var request = CreateRestRequest(HttpMethod.Post, "graphql", token);
        request.Content = JsonContent.Create(new GraphQlRequest(query, variables));
        HttpResponseMessage response;
        try { response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (TaskCanceledException) when (isWrite && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Projectへの追加結果を確認できませんでした。GitHub上で追加済みか確認してから再試行してください。");
        }
        using (response)
        {
        response.EnsureSuccessStatusCode();
        var document = await ReadJsonDocumentAsync(response, cancellationToken);
        if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            document.Dispose();
            throw new InvalidOperationException("GitHub Projects APIが失敗しました。AppのProjects権限とユーザーのアクセス権を確認してください。");
        }
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new InvalidDataException("GitHub Projects APIの応答形式が不正です。");
        }
        return document;
        }
    }

    private static HttpRequestMessage CreateRestRequest(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string ValidateRepository(string fullName)
    {
        const string prefix = "FUREQ-DEVELOP/";
        if (!fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FUREQ-DEVELOP以外のリポジトリは操作できません。");
        var name = fullName[prefix.Length..];
        if (!RepositoryNameRegex().IsMatch(name)) throw new ArgumentException("リポジトリ名が正しくありません。", nameof(fullName));
        return Uri.EscapeDataString(name);
    }

    private static void ValidateState(string state)
    {
        if (state is not ("open" or "closed" or "all")) throw new ArgumentException("stateはopen/closed/allのいずれかです。", nameof(state));
    }

    private static GitHubWorkItem MapWorkItem(IssueResponse item) =>
        new(item.Number, item.NodeId, item.Title, item.State, item.HtmlUrl, item.Body, item.LoginUser.Login, item.UpdatedAt, item.PullRequest is not null);

    private static GitHubWorkItem MapWorkItem(PullRequestResponse item) =>
        new(item.Number, item.NodeId, item.Title, item.State, item.HtmlUrl, item.Body, item.LoginUser.Login, item.UpdatedAt, true);

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        await CopyBoundedAsync(stream, buffer, cancellationToken);
        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(buffer, cancellationToken: cancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonDocumentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        await CopyBoundedAsync(stream, buffer, cancellationToken);
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
    }

    private static async Task CopyBoundedAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        var total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) return;
            total += read;
            if (total > MaximumResponseBytes) throw new InvalidDataException("GitHub API response exceeded the 4 MiB limit.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryNameRegex();

    private sealed record CreateIssueRequest(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels);
    private sealed record GraphQlRequest(
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("variables")] object Variables);
    private sealed record IssueResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("node_id")] string NodeId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("user")] User LoginUser,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
        [property: JsonPropertyName("pull_request")] JsonElement? PullRequest);
    private sealed record PullRequestResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("node_id")] string NodeId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("user")] User LoginUser,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
    private sealed record User([property: JsonPropertyName("login")] string Login);
}
