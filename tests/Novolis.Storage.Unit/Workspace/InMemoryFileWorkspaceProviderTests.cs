using Microsoft.Extensions.FileProviders;
using Novolis.IO.Workspace.Testing;

namespace Novolis.Storage.Tests.Workspace;

public sealed class InMemoryFileWorkspaceProviderTests
{
    [Test]
    public async Task Provider_exposes_files_and_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var nestedDir = Path.Combine(root, "nested");
        var fileA = Path.Combine(nestedDir, "a.json");
        var fileB = Path.Combine(root, "b.txt");
        workspace.EnsureDirectoryExists(nestedDir);
        workspace.WriteAllText(fileA, "{\"a\":1}");
        workspace.WriteAllText(fileB, "plain");

        var provider = workspace.Provider;
        var rootInfo = provider.GetFileInfo("");
        await Assert.That(rootInfo.Exists).IsTrue();
        await Assert.That(rootInfo.IsDirectory).IsTrue();

        var fileInfo = provider.GetFileInfo(Path.GetRelativePath(root, fileA));
        await Assert.That(fileInfo.Exists).IsTrue();
        await Assert.That(fileInfo.IsDirectory).IsFalse();
        await Assert.That(fileInfo.Length).IsGreaterThan(0);
        await using (var stream = fileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            await Assert.That(await reader.ReadToEndAsync()).Contains("a");
        }

        var dirContents = provider.GetDirectoryContents("");
        await Assert.That(dirContents.Exists).IsTrue();
        var names = dirContents.Select(f => f.Name).ToList();
        await Assert.That(names).Contains("nested");
        await Assert.That(names).Contains("b.txt");
    }

    [Test]
    public async Task Enumerate_files_and_entries_honors_patterns()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var dir = Path.Combine(root, "data");
        workspace.EnsureDirectoryExists(dir);
        workspace.WriteAllText(Path.Combine(dir, "one.json"), "1");
        workspace.WriteAllText(Path.Combine(dir, "two.json"), "2");
        workspace.WriteAllText(Path.Combine(dir, "skip.txt"), "x");

        var jsonFiles = workspace.EnumerateFiles(dir, "*.json").ToList();
        await Assert.That(jsonFiles.Count).IsEqualTo(2);

        var entries = workspace.EnumerateFileSystemEntries(root).Select(Path.GetFileName).ToList();
        await Assert.That(entries).Contains("data");
    }

    [Test]
    public async Task AppendAllText_and_stream_modes()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "append.txt");
        await workspace.AppendAllTextAsync(path, "hello");
        await workspace.AppendAllTextAsync(path, " world");
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("hello world");

        var appendPath = Path.Combine(root, "append-stream.txt");
        workspace.WriteAllText(appendPath, "seed");
        await using (var appendStream = workspace.CreateFileStream(appendPath, FileMode.Append, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        await using (var writer = new StreamWriter(appendStream))
        {
            await writer.WriteAsync("-tail");
        }
        await Assert.That(await workspace.ReadAllTextAsync(appendPath)).IsEqualTo("seed-tail");

        var readPath = Path.Combine(root, "read.txt");
        workspace.WriteAllText(readPath, "readonly");
        await using var readStream = workspace.CreateFileStream(readPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.None);
        using var readReader = new StreamReader(readStream);
        await Assert.That(await readReader.ReadToEndAsync()).IsEqualTo("readonly");
    }

    [Test]
    public async Task CreateFileStream_errors_and_move_conflicts()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "exists.txt");
        workspace.WriteAllText(path, "x");

        await Assert.That(() => workspace.CreateFileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
            .Throws<IOException>();
        await Assert.That(() => workspace.CreateFileStream(Path.Combine(root, "missing.txt"), FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.None))
            .Throws<FileNotFoundException>();
        await Assert.That(() => workspace.CreateFileStream(path, FileMode.Truncate, FileAccess.Read, FileShare.None, 4096, FileOptions.None))
            .Throws<NotSupportedException>();

        var dest = Path.Combine(root, "dest.txt");
        workspace.WriteAllText(dest, "taken");
        await Assert.That(() => workspace.MoveFile(path, dest, overwrite: false)).Throws<IOException>();

        workspace.Dispose();
        await Assert.That(() => workspace.FileExists(path)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DirectoryExists_detects_nested_content()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var nested = Path.Combine(root, "only-file");
        workspace.WriteAllText(Path.Combine(nested, "child.txt"), "c");
        await Assert.That(workspace.DirectoryExists(nested)).IsTrue();
        await Assert.That(workspace.DirectoryExists(Path.Combine(root, "missing"))).IsFalse();
    }

    [Test]
    public async Task Provider_watch_returns_null_change_token()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var token = workspace.Provider.Watch("*.json");
        await Assert.That(token).IsNotNull();
        await Assert.That(token.ActiveChangeCallbacks).IsFalse();
    }
}
