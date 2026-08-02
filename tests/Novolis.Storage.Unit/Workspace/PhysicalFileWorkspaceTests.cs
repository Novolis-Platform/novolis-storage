using Novolis.IO.Workspace;

namespace Novolis.Storage.Tests.Workspace;

public sealed class PhysicalFileWorkspaceTests
{
    [Test]
    public async Task Write_read_and_delete_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "nested", "sample.txt");
        workspace.EnsureDirectoryExists(Path.GetDirectoryName(path)!);

        workspace.WriteAllText(path, "hello");
        await Assert.That(workspace.FileExists(path)).IsTrue();
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("hello");

        workspace.DeleteFile(path);
        await Assert.That(workspace.FileExists(path)).IsFalse();
    }

    [Test]
    public async Task AppendAllTextAsync_concatenates_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new PhysicalFileWorkspace(root);
        var path = Path.Combine(root, "append.txt");

        await workspace.AppendAllTextAsync(path, "a");
        await workspace.AppendAllTextAsync(path, "b");
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("ab");
    }

    [Test]
    public async Task FileWorkspaceKeys_exposes_expected_names()
    {
        await Assert.That(FileWorkspaceKeys.Storage).IsEqualTo("Storage");
        await Assert.That(FileWorkspaceKeys.JsonFileEvents).IsEqualTo("JsonFileEvents");
        await Assert.That(FileWorkspaceKeys.JsonFilesStore).IsEqualTo("JsonFilesStore");
    }
}
