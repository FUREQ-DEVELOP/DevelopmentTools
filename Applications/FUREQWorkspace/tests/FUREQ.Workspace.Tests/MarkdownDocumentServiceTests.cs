using FUREQ.Workspace.Application;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class MarkdownDocumentServiceTests
{
    [Fact]
    public async Task Lists_searches_and_saves_only_markdown_with_git_diff_visible()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fureq-docs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var runner = new GitProcessRunner();
        var service = new MarkdownDocumentService();
        try
        {
            await RunGitAsync(runner, root, "init");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# Safe docs\nneedle here\n<script>alert(1)</script>\n", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, root, "add", "--", "README.md");
            await RunGitAsync(runner, root, "-c", "user.name=FUREQ Test", "-c", "user.email=fureq@example.invalid", "commit", "--message", "initial docs");
            var skippedDirectory = Path.Combine(root, "node_modules");
            Directory.CreateDirectory(skippedDirectory);
            await File.WriteAllTextAsync(Path.Combine(skippedDirectory, "dependency.md"), "needle", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "notes.txt"), "needle", TestContext.Current.CancellationToken);

            var documents = await service.ListAsync(root, TestContext.Current.CancellationToken);
            Assert.Equal("README.md", Assert.Single(documents).RelativePath);
            Assert.Contains("<script>", (await service.ReadAsync(root, "README.md", TestContext.Current.CancellationToken)).Content);
            var results = await service.SearchAsync(root, "needle", TestContext.Current.CancellationToken);
            Assert.Single(results);
            Assert.Equal(2, results[0].LineNumber);

            await service.SaveAsync(root, "README.md", "# Edited\n", TestContext.Current.CancellationToken);
            var diff = await runner.RunAsync(root, ["diff", "--", "README.md"], TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(0, diff.ExitCode);
            Assert.Contains("Edited", diff.StandardOutput);
            Assert.Equal("# Edited\n", await File.ReadAllTextAsync(Path.Combine(root, "README.md"), TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("C:/outside.md")]
    [InlineData("folder\\outside.md")]
    [InlineData("README.txt")]
    [InlineData("folder//README.md")]
    public async Task Rejects_paths_outside_the_markdown_root_or_extension(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"fureq-docs-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await new GitProcessRunner().RunAsync(root, ["init"], TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<ArgumentException>(() => new MarkdownDocumentService().ReadAsync(root, relativePath,
                TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Returns_only_known_templates()
    {
        var service = new MarkdownDocumentService();
        Assert.Contains("## 再現手順", service.GetTemplate("issue"));
        Assert.Throws<ArgumentException>(() => service.GetTemplate("https://example.invalid/template"));
    }

    private static async Task RunGitAsync(IGitProcessRunner runner, string path, params string[] arguments)
    {
        var result = await runner.RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ExitCode);
    }
}
