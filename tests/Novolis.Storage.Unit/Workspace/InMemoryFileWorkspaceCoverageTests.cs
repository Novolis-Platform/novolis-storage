using Microsoft.Extensions.FileProviders;
using Novolis.IO.Workspace.Testing;

namespace Novolis.Storage.Tests.Workspace;

public sealed class InMemoryFileWorkspaceCoverageTests
{
    [Test]
    public async Task EnumerateFiles_filters_by_pattern()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        workspace.WriteAllText(Path.Combine(root, "a.json"), "{}");
        workspace.WriteAllText(Path.Combine(root, "b.txt"), "x");

        var files = workspace.EnumerateFiles(root, "*.json").ToList();
        await Assert.That(files.Count).IsEqualTo(1);
    }

    [Test]
    public async Task EnumerateFileSystemEntries_lists_nested_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var nested = Path.Combine(root, "child");
        workspace.EnsureDirectoryExists(nested);
        workspace.WriteAllText(Path.Combine(nested, "leaf.txt"), "x");

        var entries = workspace.EnumerateFileSystemEntries(root).ToList();
        await Assert.That(entries.Any(e => e.EndsWith("child", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task ReadAllTextAsync_throws_when_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        await Assert.That(async () => await workspace.ReadAllTextAsync(Path.Combine(root, "missing.txt")))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task ReadAllBytes_throws_when_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        await Assert.That(() => workspace.ReadAllBytes(Path.Combine(root, "missing.bin")))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task WriteAllTextAsync_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "async.txt");
        await workspace.WriteAllTextAsync(path, "async-body");
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("async-body");
    }

    [Test]
    public async Task CreateFileStream_supports_truncate_open_and_open_or_create()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "modes.txt");
        workspace.WriteAllText(path, "seed");

        await using (var truncate = workspace.CreateFileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        {
            await using var writer = new StreamWriter(truncate);
            await writer.WriteAsync("truncated");
        }

        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("truncated");

        await using (var open = workspace.CreateFileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.None))
        {
            await using var writer = new StreamWriter(open);
            await writer.WriteAsync("open");
        }

        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("open");

        var fresh = Path.Combine(root, "new.txt");
        await using (var create = workspace.CreateFileStream(fresh, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
        {
            await using var writer = new StreamWriter(create);
            await writer.WriteAsync("created");
        }

        await Assert.That(await workspace.ReadAllTextAsync(fresh)).IsEqualTo("created");
    }

    [Test]
    public async Task MoveFile_without_overwrite_throws_when_destination_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var source = Path.Combine(root, "source.txt");
        var dest = Path.Combine(root, "dest.txt");
        workspace.WriteAllText(source, "s");
        workspace.WriteAllText(dest, "d");

        await Assert.That(() => workspace.MoveFile(source, dest, overwrite: false))
            .Throws<IOException>();
    }

    [Test]
    public async Task Provider_directory_contents_enumerates_entries()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        workspace.WriteAllText(Path.Combine(root, "one.txt"), "1");
        workspace.EnsureDirectoryExists(Path.Combine(root, "dir"));

        var dirWithSep = root + Path.DirectorySeparatorChar;
        using var workspace2 = new InMemoryFileWorkspace(dirWithSep);
        workspace2.WriteAllText(Path.Combine(root, "two.txt"), "2");

        var contents = workspace.Provider.GetDirectoryContents(string.Empty);
        await Assert.That(contents.Exists).IsTrue();
        await Assert.That(contents.ToList().Count).IsGreaterThan(0);

        var nested = workspace.Provider.GetDirectoryContents("dir");
        await Assert.That(nested.Exists).IsTrue();
        _ = nested.GetEnumerator().MoveNext();
    }

    [Test]
    public async Task EnumerateFiles_skips_files_outside_search_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var nested = Path.Combine(root, "nested");
        workspace.WriteAllText(Path.Combine(nested, "inner.json"), "{}");
        workspace.WriteAllText(Path.Combine(root, "outer.json"), "{}");

        var nestedOnly = workspace.EnumerateFiles(nested, "*.json").ToList();
        await Assert.That(nestedOnly.Count).IsEqualTo(1);
    }

    [Test]
    public async Task CreateFileStream_read_rejects_non_open_mode()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "read.txt");
        workspace.WriteAllText(path, "x");

        await Assert.That(() => workspace.CreateFileStream(path, FileMode.Create, FileAccess.Read, FileShare.None, 4096, FileOptions.None))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task CreateFileStream_open_missing_file_throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "missing.txt");

        await Assert.That(() => workspace.CreateFileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.None))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task CreateFileStream_unsupported_access_throws()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "bad.txt");

        await Assert.That(() => workspace.CreateFileStream(path, FileMode.Open, (FileAccess)99, FileShare.None, 4096, FileOptions.None))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task EnumerateFiles_supports_exact_pattern_and_root_with_separator()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root + Path.DirectorySeparatorChar);
        workspace.WriteAllText(Path.Combine(root, "exact.txt"), "x");
        workspace.WriteAllText(Path.Combine(root, "other.dat"), "y");

        var exact = workspace.EnumerateFiles(root, "exact.txt").ToList();
        var all = workspace.EnumerateFiles(root, "*").ToList();
        await Assert.That(exact.Count).IsEqualTo(1);
        await Assert.That(all.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Provider_directory_enumerator_non_generic()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        workspace.WriteAllText(Path.Combine(root, "one.txt"), "1");
        var contents = workspace.Provider.GetDirectoryContents(string.Empty);
        var enumerator = ((System.Collections.IEnumerable)contents).GetEnumerator();
        await Assert.That(enumerator.MoveNext()).IsTrue();
    }

    [Test]
    public async Task AppendAllTextAsync_appends_to_existing_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        using var workspace = new InMemoryFileWorkspace(root);
        var path = Path.Combine(root, "append.txt");
        workspace.WriteAllText(path, "a");
        await workspace.AppendAllTextAsync(path, "b");
        await Assert.That(await workspace.ReadAllTextAsync(path)).IsEqualTo("ab");
    }
}
