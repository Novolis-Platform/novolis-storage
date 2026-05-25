using Novolis.IO.Workspace;

namespace Novolis.Storage.Json;

internal static class JsonFilesStartupValidator
{
    /// <summary>
    /// Fail-fast: ensure root exists or can be created, verify enumeration and (if not read-only) write/delete and atomic replace.
    /// </summary>
    public static void Validate(JsonFilesOptions options, IFileWorkspace workspace, out IDisposable? processLock)
    {
        processLock = null;
        var root = Path.GetFullPath(options.RootPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("JsonFilesOptions.RootPath must be set.");

        if (!workspace.DirectoryExists(root))
        {
            if (options.ReadOnly || !options.CreateIfMissing)
                throw new InvalidOperationException($"Root path does not exist and cannot be created: {root}");
            workspace.EnsureDirectoryExists(root);
        }

        _ = workspace.EnumerateFileSystemEntries(root).Take(1).ToList();

        if (!options.ReadOnly)
        {
            var probe = Path.Combine(root, ".probe_" + Guid.NewGuid().ToString("N"));
            try
            {
                workspace.WriteAllText(probe, "x");
                var read = workspace.ReadAllTextAsync(probe).GetAwaiter().GetResult();
                if (read != "x")
                    throw new InvalidOperationException($"Read-back failed in root: {root}");
            }
            finally
            {
                if (workspace.FileExists(probe))
                    workspace.DeleteFile(probe);
            }

            var final = Path.Combine(root, ".atomic_" + Guid.NewGuid().ToString("N"));
            var tmp = final + ".tmp";
            try
            {
                workspace.WriteAllText(tmp, "a");
                using (var fs = workspace.CreateFileStream(tmp, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096,
                           FileOptions.None))
                    fs.Flush();

                workspace.MoveFile(tmp, final, overwrite: true);
                if (!workspace.FileExists(final) || workspace.ReadAllTextAsync(final).GetAwaiter().GetResult() != "a")
                    throw new InvalidOperationException($"Atomic replace check failed in root: {root}");
            }
            finally
            {
                if (workspace.FileExists(tmp))
                    workspace.DeleteFile(tmp);
                if (workspace.FileExists(final))
                    workspace.DeleteFile(final);
            }
        }

        if (!options.ReadOnly && options.UseProcessLock)
        {
            var lockPath = Path.Combine(root, ".store.lock");
            var stream = workspace.CreateFileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096,
                FileOptions.None);
            processLock = new ProcessLockHolder(stream);
        }
    }

    private sealed class ProcessLockHolder(Stream stream) : IDisposable
    {
        public void Dispose() => stream.Dispose();
    }
}
