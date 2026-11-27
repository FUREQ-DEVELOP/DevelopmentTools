using FUREQ.Workspace.Application;
using FUREQ.Workspace.Domain;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitSubmoduleServiceTests
{
    [Fact]
    public async Task Reads_nested_pinned_graph_and_initializes_missing_modules_recursively()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fureq-submodule-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var leaf = Path.Combine(root, "leaf");
        var middle = Path.Combine(root, "middle");
        var parent = Path.Combine(root, "parent");
        Directory.CreateDirectory(leaf);
        Directory.CreateDirectory(middle);
        Directory.CreateDirectory(parent);
        var processRunner = new GitProcessRunner();
        var runner = new AllowLocalSubmoduleRunner(processRunner);
        var service = new GitSubmoduleService(runner, new LocalRemotePolicy());

        try
        {
            await InitRepositoryAsync(runner, leaf);
            await File.WriteAllTextAsync(Path.Combine(leaf, "leaf.txt"), "leaf", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, leaf, "add", "--", "leaf.txt");
            await RunGitAsync(runner, leaf, "commit", "--message", "leaf");

            await InitRepositoryAsync(runner, middle);
            await File.WriteAllTextAsync(Path.Combine(middle, "middle.txt"), "middle", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, middle, "add", "--", "middle.txt");
            await RunGitAsync(runner, middle, "commit", "--message", "middle base");
            await RunGitAsync(runner, middle, "submodule", "add", leaf, "deps/leaf");
            await RunGitAsync(runner, middle, "commit", "--all", "--message", "middle with leaf");

            await InitRepositoryAsync(runner, parent);
            await RunGitAsync(runner, parent, "remote", "add", "origin", "https://github.com/FUREQ-DEVELOP/parent.git");
            await RunGitAsync(runner, parent, "submodule", "add", middle, "deps/middle");
            await RunGitAsync(runner, parent, "commit", "--all", "--message", "parent with middle");

            var initial = await service.GetGraphAsync(parent, TestContext.Current.CancellationToken);
            Assert.Equal(2, initial.Entries.Count);
            var middleEntry = Assert.Single(initial.Entries, entry => entry.RelativePath == "deps/middle");
            var leafEntry = Assert.Single(initial.Entries, entry => entry.RelativePath == "deps/middle/deps/leaf");
            Assert.True(middleEntry.IsInitialized);
            Assert.False(leafEntry.IsInitialized);
            Assert.Equal(40, leafEntry.PinnedCommit.Length);

            await RunGitAsync(runner, parent, "submodule", "deinit", "--force", "--", "deps/middle");
            var initialized = await service.InitializeMissingRecursivelyAsync(parent, TestContext.Current.CancellationToken);

            Assert.Equal(2, initialized.Entries.Count);
            Assert.All(initialized.Entries, entry => Assert.True(entry.IsInitialized));
            Assert.All(initialized.Entries, entry => Assert.Equal(entry.PinnedCommit, entry.CurrentCommit));
            Assert.Contains(initialized.Entries, entry => entry.RelativePath == "deps/middle/deps/leaf");

            var middlePath = Path.Combine(parent, "deps", "middle");
            var priorCommit = await runner.RunAsync(middlePath, ["rev-parse", "HEAD~1"], TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.Equal(0, priorCommit.ExitCode);
            await RunGitAsync(runner, middlePath, "switch", "--detach", priorCommit.StandardOutput.Trim());
            var updatePreview = await service.GetGraphAsync(parent, TestContext.Current.CancellationToken);
            var mismatch = Assert.Single(updatePreview.PinnedUpdatePreview);
            Assert.Equal("deps/middle", mismatch.RelativePath);
            Assert.Equal("deps/middle", Assert.Single(updatePreview.Entries).RelativePath);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task InitRepositoryAsync(IGitProcessRunner runner, string path)
    {
        await RunGitAsync(runner, path, "init");
        await RunGitAsync(runner, path, "config", "user.name", "FUREQ Test");
        await RunGitAsync(runner, path, "config", "user.email", "fureq-test@example.invalid");
    }

    private static async Task RunGitAsync(IGitProcessRunner runner, string path, params string[] arguments)
    {
        var result = await runner.RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, $"git exited {result.ExitCode}: {result.StandardError}");
    }

    private sealed class AllowLocalSubmoduleRunner(IGitProcessRunner inner) : IGitProcessRunner
    {
        public Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
            TimeSpan timeout, CancellationToken cancellationToken = default, bool disableHooks = false,
            bool disableFsMonitor = false, bool useGitCredentialManager = false, bool isolateCheckoutConfiguration = false)
        {
            if (arguments.Count > 0 && arguments[0] == "submodule")
                arguments = ["-c", "protocol.file.allow=always", .. arguments];
            return inner.RunAsync(workingDirectory, arguments, timeout, cancellationToken, disableHooks,
                disableFsMonitor, useGitCredentialManager, isolateCheckoutConfiguration);
        }

        public Task<string?> GetGitHubCredentialUsernameAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            inner.GetGitHubCredentialUsernameAsync(workingDirectory, cancellationToken);
    }

    private sealed class LocalRemotePolicy : IGitRemotePolicy
    {
        public string GetRepositoryFullName(string remoteUrl) => "FUREQ-DEVELOP/LocalTest";
    }
}
