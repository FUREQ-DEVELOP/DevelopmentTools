using FUREQ.Workspace.Domain;

namespace FUREQ.Workspace.Application;

public interface IMarkdownDocumentService
{
    Task<IReadOnlyList<MarkdownDocument>> ListAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<MarkdownDocument> ReadAsync(string repositoryPath, string relativePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarkdownSearchResult>> SearchAsync(string repositoryPath, string query, CancellationToken cancellationToken = default);
    Task SaveAsync(string repositoryPath, string relativePath, string content, CancellationToken cancellationToken = default);
    string GetTemplate(string templateName);
}
