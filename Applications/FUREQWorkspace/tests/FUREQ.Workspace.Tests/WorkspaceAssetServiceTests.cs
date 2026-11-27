using FUREQ.Workspace.Infrastructure;
using Xunit;

namespace FUREQ.Workspace.Tests;

public sealed class WorkspaceAssetServiceTests
{
    [Fact]
    public async Task Lists_images_and_lfs_pointers_without_fetching_lfs_objects()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fureq-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await new GitProcessRunner().RunAsync(root, ["init"], TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            var imagePath = Path.Combine(root, "texture.png");
            await File.WriteAllBytesAsync(imagePath, [1, 2, 3], TestContext.Current.CancellationToken);
            const string oid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var pointerPath = Path.Combine(root, "model.bin");
            var pointer = $"version https://git-lfs.github.com/spec/v1\r\noid sha256:{oid}\r\nsize 123456\r\n";
            await File.WriteAllTextAsync(pointerPath, pointer, TestContext.Current.CancellationToken);
            var dependency = Path.Combine(root, "node_modules");
            Directory.CreateDirectory(dependency);
            await File.WriteAllTextAsync(Path.Combine(dependency, "hidden.png"), "ignored", TestContext.Current.CancellationToken);

            var assets = await new WorkspaceAssetService().ListAsync(root, TestContext.Current.CancellationToken);

            Assert.Equal(2, assets.Count);
            var image = Assert.Single(assets, asset => asset.RelativePath == "texture.png");
            Assert.True(image.IsImage);
            Assert.False(image.IsGitLfsPointer);
            var lfs = Assert.Single(assets, asset => asset.RelativePath == "model.bin");
            Assert.True(lfs.IsGitLfsPointer);
            Assert.Equal(oid, lfs.LfsObjectId);
            Assert.Equal(123456, lfs.LfsObjectSize);
            Assert.Equal(pointer, await File.ReadAllTextAsync(pointerPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
