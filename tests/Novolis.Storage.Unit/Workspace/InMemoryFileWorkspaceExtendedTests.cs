using Novolis.IO.Workspace.Testing;

namespace Novolis.Storage.Tests.Workspace;

public sealed class InMemoryFileWorkspaceExtendedTests
{
    [Test]
    public async Task Delete_file_removes_entry()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "file.txt");
        workspace.WriteAllText(path, "x");
        await Assert.That(workspace.FileExists(path)).IsTrue();
        workspace.DeleteFile(path);
        await Assert.That(workspace.FileExists(path)).IsFalse();
    }

    [Test]
    public async Task Move_file_and_read_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var source = Path.Combine(root, "a.bin");
        var dest = Path.Combine(root, "b.bin");
        var bytes = new byte[] { 9, 8, 7 };
        workspace.WriteAllBytes(source, bytes);
        workspace.MoveFile(source, dest, overwrite: true);

        await Assert.That(workspace.ReadAllBytes(dest)).IsEquivalentTo(bytes);
        await Assert.That(workspace.FileExists(source)).IsFalse();
    }

    [Test]
    public async Task CreateFileStream_writes_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "stream.txt");

        await using (var stream = workspace.CreateFileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        {
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("memory-stream");
        }

        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("memory-stream");
        await Assert.That(workspace.Provider).IsNotNull();
    }
}
