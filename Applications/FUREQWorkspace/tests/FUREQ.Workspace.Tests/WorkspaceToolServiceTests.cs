using System.Text.Json;
using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class WorkspaceToolServiceTests
{
    [Fact]
    public async Task Discovers_only_valid_executables_inside_approved_checkout()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"fureq-tools-{Guid.NewGuid():N}");
        var root = Path.Combine(temp, "DevelopmentTools");
        var tools = Path.Combine(root, "Tools");
        var app = Path.Combine(root, "Applications", "FUREQWorkspace");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(app);
        try
        {
            var executable = Path.Combine(tools, "safe.exe");
            await File.WriteAllBytesAsync(executable, [0x4d, 0x5a], TestContext.Current.CancellationToken);
            var outside = Path.Combine(temp, "outside.exe");
            await File.WriteAllBytesAsync(outside, [0x4d, 0x5a], TestContext.Current.CancellationToken);
            await WriteManifestAsync(Path.Combine(tools, "valid.fureq-tool.json"), "Tools/safe.exe");
            await WriteManifestAsync(Path.Combine(tools, "escape.fureq-tool.json"), "../outside.exe");
            await WriteManifestAsync(Path.Combine(tools, "unknown.fureq-tool.json"), "Tools/safe.exe", includeUnknown: true);
            await File.WriteAllTextAsync(Path.Combine(tools, "bad.fureq-tool.json"), "{", TestContext.Current.CancellationToken);

            var found = await new WorkspaceToolService().DiscoverAsync(root, TestContext.Current.CancellationToken);

            var tool = Assert.Single(found);
            Assert.Equal("safe-tool", tool.ToolId);
            Assert.Equal(Path.GetFullPath(executable), tool.EntryPoint);
            Assert.Equal(["--mode", "inspect"], tool.Arguments);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_a_directory_that_is_not_the_approved_checkout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fureq-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkspaceToolService().DiscoverAsync(root, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task WriteManifestAsync(string path, string entryPoint, bool includeUnknown = false)
    {
        var value = new Dictionary<string, object>
        {
            ["schemaVersion"] = 1,
            ["toolId"] = "safe-tool",
            ["displayName"] = "Safe tool",
            ["description"] = "Inspects input",
            ["version"] = "1.0",
            ["entryPoint"] = entryPoint,
            ["executionType"] = "executable",
            ["sideEffectClassification"] = "read-only",
            ["timeoutSeconds"] = 30,
            ["arguments"] = new[] { "--mode", "inspect" }
        };
        if (includeUnknown) value["launchShell"] = true;
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value), TestContext.Current.CancellationToken);
    }
}
