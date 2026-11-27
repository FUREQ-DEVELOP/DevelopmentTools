namespace FUREQ.Workspace.Domain;

public sealed record MarkdownDocument(string RelativePath, string FullPath, string Content, long Length, DateTimeOffset LastModifiedUtc);

public sealed record MarkdownSearchResult(string RelativePath, int LineNumber, string Preview);
