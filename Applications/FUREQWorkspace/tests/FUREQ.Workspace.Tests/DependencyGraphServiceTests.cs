using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class DependencyGraphServiceTests
{
    [Fact]
    public async Task Keeps_declared_sdk_and_api_edges_separate_from_gitlinks()
    {
        var root = CreateRepository();
        try
        {
            var directory = Path.Combine(root, ".fureq");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "dependencies.json"), """
                { "schemaVersion": 1, "dependencies": [
                  { "repository": "FUREQ-DEVELOP/RenderingLibrary", "type": "sdk", "sdkVersion": "2.1.0", "apiCompatibility": "2.x", "releaseUrl": "https://github.com/FUREQ-DEVELOP/RenderingLibrary/releases/tag/v2.1.0" }
                ] }
                """, TestContext.Current.CancellationToken);
            var gitlinks = new SubmoduleGraph([new SubmoduleGraphEntry("vendor/RenderingLibrary", "FUREQ-DEVELOP/RenderingLibrary", "abc", "abc", true)]);

            var result = await new DependencyGraphService(new FakeSubmoduleService(gitlinks)).LoadAsync(root, TestContext.Current.CancellationToken);

            Assert.Same(gitlinks, result.Submodules);
            var dependency = Assert.Single(result.DeclaredDependencies);
            Assert.Equal("sdk", dependency.DependencyType);
            Assert.Equal("2.1.0", dependency.SdkVersion);
            Assert.Equal("2.x", dependency.ApiCompatibility);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Rejects_out_of_organization_or_duplicate_declared_dependencies()
    {
        var root = CreateRepository();
        try
        {
            var directory = Path.Combine(root, ".fureq");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "dependencies.json"), """
                { "schemaVersion": 1, "dependencies": [
                  { "repository": "example/Untrusted", "type": "api" }
                ] }
                """, TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidDataException>(() => new DependencyGraphService(new FakeSubmoduleService(new SubmoduleGraph([])))
                .LoadAsync(root, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fureq-dependencies-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeSubmoduleService(SubmoduleGraph graph) : ISubmoduleService
    {
        public Task<SubmoduleGraph> GetGraphAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.FromResult(graph);
        public Task<SubmoduleGraph> InitializeMissingRecursivelyAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.FromResult(graph);
    }
}
