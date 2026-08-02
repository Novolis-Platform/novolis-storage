using Novolis.IO.Workspace;

namespace Novolis.Storage.Tests.Workspace;

public sealed class PhysicalFileWorkspaceAdditionalTests
{
    [Test]
    public async Task Provider_and_dispose()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "x.txt");
        workspace.WriteAllText(path, "x");
        await Assert.That(workspace.Provider.GetFileInfo("x.txt").Exists).IsTrue();
        workspace.Dispose();
        workspace.Dispose();
    }

    [Test]
    public async Task CreateFileStream_read_existing_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "read.txt");
        workspace.WriteAllText(path, "payload");
        await using var stream = workspace.CreateFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);
        using var reader = new StreamReader(stream);
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("payload");
    }

    [Test]
    public async Task WriteAllBytes_and_directory_exists_for_nested_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "nested", "data.bin");
        workspace.EnsureDirectoryExists(Path.GetDirectoryName(path)!);
        workspace.WriteAllBytes(path, [5, 6, 7]);
        await Assert.That(workspace.FileExists(path)).IsTrue();
        await Assert.That(workspace.DirectoryExists(Path.GetDirectoryName(path)!)).IsTrue();
        await Assert.That(workspace.ReadAllBytes(path)[0]).IsEqualTo((byte)5);
    }
}
