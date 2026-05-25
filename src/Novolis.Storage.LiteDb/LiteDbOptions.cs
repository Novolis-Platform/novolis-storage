namespace Novolis.Storage.LiteDb;

/// <summary>
/// Configuration for the LiteDB storage provider.
/// </summary>
public sealed class LiteDbOptions
{
    /// <summary>Path to the database file (used to build Filename= in connection string).</summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>Optional AES encryption password.</summary>
    public string? Password { get; set; }

    /// <summary>Number of write lock stripes for concurrency (default 4096).</summary>
    public int WriteLockStripes { get; set; } = 4096;
}
