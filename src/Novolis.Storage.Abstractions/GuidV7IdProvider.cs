namespace Novolis.Storage.Abstractions;

/// <summary>
/// Generates sortable, time-based GUIDs using UUID version 7 (RFC 9562).
/// </summary>
public sealed class GuidV7IdProvider : IIdProvider
{
    /// <inheritdoc />
    public Guid NewId() => Guid.CreateVersion7();
}
