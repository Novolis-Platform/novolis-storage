using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class TypedSqliteEntity : IHasId
{
    public Guid Id { get; set; }
    public int IntVal { get; set; }
    public long LongVal { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Flag { get; set; }
    public DateTime CreatedAt { get; set; }
}
