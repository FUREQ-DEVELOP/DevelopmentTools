using FUREQ.Workspace.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class WorkspaceInventoryTests
{
    [Fact]
    public async Task Rejects_duplicate_canonical_workspace_root()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var inventory = await CreateInventoryAsync(root);
            await inventory.CreateProfileAsync("Engineering", "Engine", root, TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() => inventory.CreateProfileAsync(
                "Duplicate", "Art", Path.Combine(root, "."), TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Inventory_is_immediate_only_and_repeat_scans_do_not_duplicate_paths()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var gitDirectory = Directory.CreateDirectory(Path.Combine(root, "Game"));
            Directory.CreateDirectory(Path.Combine(gitDirectory.FullName, ".git"));
            var worktree = Directory.CreateDirectory(Path.Combine(root, "Tools"));
            await File.WriteAllTextAsync(Path.Combine(worktree.FullName, ".git"), "gitdir: ../.git/worktrees/tools",
                TestContext.Current.CancellationToken);
            var nested = Directory.CreateDirectory(Path.Combine(root, "Parent", "NestedRepo"));
            Directory.CreateDirectory(Path.Combine(nested.FullName, ".git"));
            Directory.CreateDirectory(Path.Combine(root, "NotARepository"));

            var inventory = await CreateInventoryAsync(root);
            var profile = await inventory.CreateProfileAsync("Engineering", "Engine", root, TestContext.Current.CancellationToken);

            Assert.Equal(2, await inventory.ScanRepositoriesAsync(profile.Id, TestContext.Current.CancellationToken));
            Assert.Equal(2, await inventory.ScanRepositoriesAsync(profile.Id, TestContext.Current.CancellationToken));
            Directory.Delete(gitDirectory.FullName, recursive: true);
            Assert.Equal(1, await inventory.ScanRepositoriesAsync(profile.Id, TestContext.Current.CancellationToken));
            var results = await inventory.GetRepositoriesAsync(profile.Id, TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "Game", "Tools" }, results.Select(item => item.DirectoryName).OrderBy(name => name));
            Assert.Equal(2, results.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.False(results.Single(item => item.DirectoryName == "Game").IsPresent);
            Assert.True(results.Single(item => item.DirectoryName == "Tools").IsPresent);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<SqliteWorkspaceInventory> CreateInventoryAsync(string root)
    {
        var database = new WorkspaceDatabase(Path.Combine(root, "test.db"));
        await new DatabaseInitializer(database).InitializeAsync(TestContext.Current.CancellationToken);
        return new SqliteWorkspaceInventory(database);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fureq-inventory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
