using Novolis.IO.Workspace;

namespace Novolis.Storage.Tests.Workspace;

public sealed class PhysicalFileWorkspaceCoverageTests
{
    [Test]
    public async Task Append_delete_and_open_or_create()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "append.txt");
        await workspace.AppendAllTextAsync(path, "a");
        await workspace.AppendAllTextAsync(path, "b");
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("ab");

        workspace.DeleteFile(path);
        await Assert.That(workspace.FileExists(path)).IsFalse();

        await using (var stream = workspace.CreateFileStream(
                         Path.Combine(root, "create.txt"),
                         FileMode.OpenOrCreate,
                         FileAccess.Write,
                         FileShare.None,
                         4096,
                         FileOptions.None))
        {
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("created");
        }

        await Assert.That(await workspace.ReadAllTextAsync(Path.Combine(root, "create.txt"))).IsEqualTo("created");
    }

    [Test]
    public async Task EnsureDirectoryExists_creates_nested_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var nested = Path.Combine(root, "a", "b");
        workspace.EnsureDirectoryExists(nested);
        await Assert.That(workspace.DirectoryExists(nested)).IsTrue();
    }
}
