using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableScalars : IHasId
{
    public Guid Id { get; set; }

    public string? Text { get; set; }

    public bool Flag { get; set; }

    public bool? Optional { get; set; }

    public byte B { get; set; }

    public sbyte Sb { get; set; }

    public short S { get; set; }

    public ushort Us { get; set; }

    public int I { get; set; }

    public int? Score { get; set; }

    public uint Ui { get; set; }

    public long L { get; set; }

    public float F { get; set; }

    public double D { get; set; }

    public Guid Token { get; set; }

    public DateTime When { get; set; }

    public DateTimeOffset WhenOffset { get; set; }

    public AzureTableStatus Status { get; set; }

    public int ReadOnly => 1;

    public int this[int index]
    {
        get => 0;
        set { _ = value; }
    }
}
