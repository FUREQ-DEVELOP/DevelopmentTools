using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Infrastructure;

public sealed partial class GitHubPullRequestService(
    HttpClient httpClient,
    IGitRepositoryOperations gitOperations,
    IGitHubAuthorization authorization) : IGitHubPullRequestService
{
    public async Task<PullRequestLink> CreateAsync(string repositoryPath, string title, string body, string baseBranch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 256) throw new ArgumentException("PRタイトルは1〜256文字で入力してください。", nameof(title));
        if (body.Length > 10000) throw new ArgumentException("PR本文は10,000文字以内で入力してください。", nameof(body));
        if (!BranchRegex().IsMatch(baseBranch) || baseBranch.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("ベースブランチ名が正しくありません。", nameof(baseBranch));

        var snapshot = await gitOperations.GetStatusAsync(repositoryPath, cancellationToken);
        if (snapshot.IsDirty || snapshot.HasConflicts || string.IsNullOrEmpty(snapshot.Branch)
            || snapshot.Branch is "main" or "master" || string.IsNullOrEmpty(snapshot.Upstream)
            || !snapshot.Upstream.StartsWith("origin/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PRを作成するには、originへPush済みの変更ブランチを選び、作業ツリーをクリーンにしてください。");
        }

        var accessToken = await authorization.GetAccessTokenAsync(cancellationToken)
            ?? throw new InvalidOperationException("PR作成にはGitHubでのサインインが必要です。");
        var repositoryName = snapshot.RepositoryFullName[("FUREQ-DEVELOP/".Length)..];
        using var request = new HttpRequestMessage(HttpMethod.Post, $"repos/FUREQ-DEVELOP/{Uri.EscapeDataString(repositoryName)}/pulls")
        {
            Content = JsonContent.Create(new CreatePullRequest(title.Trim(), body, snapshot.Branch, baseBranch))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("PR作成の結果を確認できませんでした。GitHubで作成済みか確認してから再試行してください。");
        }

        using (response)
        {
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PullRequestResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("GitHubからPR情報を取得できませんでした。");
            return new PullRequestLink(result.HtmlUrl, result.Number, result.Title);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._/-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex BranchRegex();

    private sealed record CreatePullRequest(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("head")] string Head,
        [property: JsonPropertyName("base")] string Base);

    private sealed record PullRequestResponse(
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("title")] string Title);
}
