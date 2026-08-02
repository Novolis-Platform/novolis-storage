using Novolis.IO.Workspace.Testing;

namespace Novolis.Storage.Tests.Workspace;

public sealed class InMemoryFileWorkspaceTests
{
    [Test]
    public async Task Write_read_roundtrip_without_disk_io()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "data.json");

        workspace.WriteAllText(path, "{\"ok\":true}");
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("{\"ok\":true}");
        await Assert.That(PhysicalFileExistsOnDisk(path)).IsFalse();
    }

    [Test]
    public async Task Paths_are_case_insensitive()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var lower = Path.Combine(root, "file.txt");
        var upper = Path.Combine(root, "FILE.TXT");

        workspace.WriteAllText(lower, "x");
        await Assert.That(workspace.FileExists(upper)).IsTrue();
        await Assert.That(await workspace.ReadAllTextAsync(upper)).IsEqualTo("x");
    }

    private static bool PhysicalFileExistsOnDisk(string path) => File.Exists(path);
}
