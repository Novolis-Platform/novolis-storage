using Novolis.Storage.Abstractions;
using Novolis.Storage.Abstractions.Events;

namespace Novolis.Storage.Tests.Abstractions;

public sealed class SortableBase62Tests
{
    [Test]
    public async Task Encode_zero_returns_single_zero_char()
    {
        await Assert.That(SortableBase62.Encode(0)).IsEqualTo("0");
    }

    [Test]
    public async Task Encode_decode_roundtrip()
    {
        const long ticks = 638_000_000_000_000_000L;
        var encoded = SortableBase62.Encode(ticks);
        await Assert.That(SortableBase62.Decode(encoded)).IsEqualTo(ticks);
    }

    [Test]
    public async Task Encode_negative_throws()
    {
        await Assert.That(() => SortableBase62.Encode(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Decode_invalid_char_throws()
    {
        await Assert.That(() => SortableBase62.Decode("abc!")).Throws<FormatException>();
    }

    [Test]
    public async Task Decode_empty_throws()
    {
        await Assert.That(() => SortableBase62.Decode("   ")).Throws<ArgumentException>();
    }
}
