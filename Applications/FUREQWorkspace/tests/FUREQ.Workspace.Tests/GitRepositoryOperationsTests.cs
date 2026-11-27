using FUREQ.Workspace.Application;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class GitRepositoryOperationsTests
{
    [Fact]
    public async Task Supports_branch_commit_push_fetch_and_fast_forward_pull_without_running_hooks()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var remote = Path.Combine(root, "origin.git");
            var working = Path.Combine(root, "working");
            var peer = Path.Combine(root, "peer");
            var runner = new GitProcessRunner();
            await RunGitAsync(runner, root, "init", "--bare", "--initial-branch=main", remote);
            await RunGitAsync(runner, root, "clone", remote, working);
            await ConfigureIdentityAsync(runner, working);
            await File.WriteAllTextAsync(Path.Combine(working, "README.md"), "initial\n", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, working, "add", "--", "README.md");
            await RunGitAsync(runner, working, "commit", "--message", "initial");
            await RunGitAsync(runner, working, "push", "--set-upstream", "origin", "main");

            var operations = new GitRepositoryOperations(runner, new LocalTestRemotePolicy(), new AllowGitWriteIdentity());
            var mainSnapshot = await operations.GetStatusAsync(working, TestContext.Current.CancellationToken);
            Assert.Equal("main", mainSnapshot.Branch);
            Assert.False(mainSnapshot.IsDirty);
            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.PushAsync(working, TestContext.Current.CancellationToken));

            await operations.CreateBranchAsync(working, "feature/inventory", TestContext.Current.CancellationToken);
            await File.AppendAllTextAsync(Path.Combine(working, "README.md"), "feature\n", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, working, "add", "--", "README.md");
            var hook = Path.Combine(working, ".git", "hooks", "post-commit");
            await File.WriteAllTextAsync(hook, "#!/bin/sh\necho executed > hook-was-run.txt\n", TestContext.Current.CancellationToken);
            await operations.CommitStagedChangesAsync(working, "feature work", TestContext.Current.CancellationToken);
            Assert.False(File.Exists(Path.Combine(working, "hook-was-run.txt")));
            await operations.PushAsync(working, TestContext.Current.CancellationToken);
            Assert.Contains("feature/inventory", await operations.GetBranchesAsync(working, TestContext.Current.CancellationToken));

            await RunGitAsync(runner, root, "clone", remote, peer);
            await ConfigureIdentityAsync(runner, peer);
            await RunGitAsync(runner, peer, "switch", "--track", "--create", "feature/inventory", "origin/feature/inventory");
            await File.WriteAllTextAsync(Path.Combine(peer, "peer.txt"), "peer update\n", TestContext.Current.CancellationToken);
            await RunGitAsync(runner, peer, "add", "--", "peer.txt");
            await RunGitAsync(runner, peer, "commit", "--message", "peer update");
            await RunGitAsync(runner, peer, "push", "origin", "feature/inventory");

            var fetched = await operations.FetchAsync(working, TestContext.Current.CancellationToken);
            Assert.Equal(1, fetched.Behind);
            var pulled = await operations.PullFastForwardAsync(working, TestContext.Current.CancellationToken);
            Assert.False(pulled.IsDirty);
            Assert.True(File.Exists(Path.Combine(working, "peer.txt")));

            await File.WriteAllTextAsync(Path.Combine(working, "local.txt"), "uncommitted\n", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => operations.PullFastForwardAsync(working, TestContext.Current.CancellationToken));
            Assert.Equal("uncommitted\n", await File.ReadAllTextAsync(Path.Combine(working, "local.txt"), TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Theory]
    [InlineData("https://github.com/FUREQ-DEVELOP/RenderingWorkspace.git", "FUREQ-DEVELOP/RenderingWorkspace")]
    [InlineData("https://github.com/fureq-develop/Repo_Name", "FUREQ-DEVELOP/Repo_Name")]
    public void Accepts_only_canonical_FUREQ_DEVELOP_HTTPS_origins(string url, string expected)
    {
        Assert.Equal(expected, new GitHubOrganizationRemotePolicy().GetRepositoryFullName(url));
    }

    [Theory]
    [InlineData("git@github.com:FUREQ-DEVELOP/repo.git")]
    [InlineData("https://user:secret@github.com/FUREQ-DEVELOP/repo.git")]
    [InlineData("https://github.com/another-org/repo.git")]
    [InlineData("https://github.com/FUREQ-DEVELOP/repo.git/other")]
    [InlineData("https://github.com/FUREQ-DEVELOP/repo.git?token=secret")]
    [InlineData("https://github.com:8443/FUREQ-DEVELOP/repo.git")]
    public void Rejects_unexpected_or_credential_bearing_origins(string url)
    {
        Assert.Throws<InvalidOperationException>(() => new GitHubOrganizationRemotePolicy().GetRepositoryFullName(url));
    }

    private static async Task ConfigureIdentityAsync(IGitProcessRunner runner, string path)
    {
        await RunGitAsync(runner, path, "config", "user.name", "FUREQ Test");
        await RunGitAsync(runner, path, "config", "user.email", "fureq-test@example.invalid");
    }

    private static async Task RunGitAsync(IGitProcessRunner runner, string path, params string[] arguments)
    {
        var result = await runner.RunAsync(path, arguments, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(result.ExitCode == 0, $"git exited {result.ExitCode}: {result.StandardError}");
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fureq-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }

    private sealed class LocalTestRemotePolicy : IGitRemotePolicy
    {
        public string GetRepositoryFullName(string remoteUrl) => "FUREQ-DEVELOP/LocalTest";
    }

    private sealed class AllowGitWriteIdentity : IGitWriteIdentityValidator
    {
        public Task ValidateAsync(string repositoryPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
