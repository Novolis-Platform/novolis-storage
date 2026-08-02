using Novolis.IO.Workspace;

namespace Novolis.Storage.Tests.Workspace;

public sealed class PhysicalFileWorkspaceExtendedTests
{
    [Test]
    public async Task Read_write_bytes_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "data.bin");
        var bytes = new byte[] { 1, 2, 3, 4 };

        workspace.WriteAllBytes(path, bytes);
        await Assert.That(workspace.ReadAllBytes(path)).IsEquivalentTo(bytes);
    }

    [Test]
    public async Task Move_file_renames_on_disk()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var source = Path.Combine(root, "source.txt");
        var dest = Path.Combine(root, "dest.txt");

        workspace.WriteAllText(source, "move-me");
        workspace.MoveFile(source, dest, overwrite: true);
        await Assert.That(workspace.FileExists(source)).IsFalse();
        await Assert.That(await workspace.ReadAllTextAsync(dest)).IsEqualTo("move-me");
    }

    [Test]
    public async Task Enumerate_files_and_static_helpers()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "a.txt");
        workspace.WriteAllText(path, "a");

        await Assert.That(workspace.EnumerateFiles(root, "*.txt").Count()).IsEqualTo(1);
        await Assert.That(workspace.EnumerateFileSystemEntries(root).Count()).IsGreaterThan(0);
        await Assert.That(PhysicalFileWorkspace.FileExistsOnDisk(path)).IsTrue();
        await Assert.That(PhysicalFileWorkspace.DirectoryExistsOnDisk(root)).IsTrue();
        await Assert.That(PhysicalFileWorkspace.ReadAllTextOnDisk(path)).IsEqualTo("a");
        await Assert.That(workspace.Provider).IsNotNull();
        await Assert.That(workspace.RootPath).IsEqualTo(Path.GetFullPath(root));
    }

    [Test]
    public async Task CreateFileStream_supports_read_write()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "stream.txt");

        await using (var stream = workspace.CreateFileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        {
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("streamed");
        }

        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("streamed");
    }
}
