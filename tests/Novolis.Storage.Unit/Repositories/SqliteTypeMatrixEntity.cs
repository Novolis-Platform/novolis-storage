using System.Globalization;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class SqliteTypeMatrixEntity : IHasId
{
    public Guid Id { get; set; }
    public int IntVal { get; set; }
    public long LongVal { get; set; }
    public short ShortVal { get; set; }
    public byte ByteVal { get; set; }
    public uint UIntVal { get; set; }
    public ulong ULongVal { get; set; }
    public ushort UShortVal { get; set; }
    public sbyte SByteVal { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid? OptionalGuid { get; set; }
    public bool Flag { get; set; }
    public bool? OptionalFlag { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? OptionalDate { get; set; }
    public decimal Amount { get; set; }
    public decimal? OptionalAmount { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan? OptionalDuration { get; set; }
    public DateTimeOffset Offset { get; set; }
    public DateTimeOffset? OptionalOffset { get; set; }
}
