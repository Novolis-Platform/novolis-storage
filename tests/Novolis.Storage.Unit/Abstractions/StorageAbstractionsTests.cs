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

public sealed class SessionIdTests
{
    [Test]
    public async Task CreateNow_produces_parseable_id()
    {
        var session = SessionId.CreateNow();
        await Assert.That(session.Base62).IsNotNullOrEmpty();
        await Assert.That(session.Ticks).IsGreaterThan(0);
        await Assert.That(SessionId.Parse(session.Base62).Ticks).IsEqualTo(session.Ticks);
    }

    [Test]
    public async Task FromTicks_and_ToString()
    {
        const long ticks = 638_000_000_000_000_000L;
        var session = SessionId.FromTicks(ticks);
        await Assert.That(session.ToString()).IsEqualTo(session.Base62);
        await Assert.That(session.Ticks).IsEqualTo(ticks);
    }
}

public sealed class StreamIdTests
{
    [Test]
    public async Task FromSession_maps_ticks()
    {
        var session = SessionId.FromTicks(12345);
        var stream = StreamId.FromSession(session);
        await Assert.That(stream.Value).IsEqualTo(12345);
        await Assert.That(stream.ToString()).IsEqualTo("12345");
    }
}

public sealed class EventEnvelopeTests
{
    [Test]
    public async Task Record_stores_payload_and_timestamp()
    {
        var session = SessionId.FromTicks(99);
        var stream = StreamId.FromSession(session);
        var timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var envelope = new EventEnvelope(stream, new { Name = "test" }, timestamp);

        await Assert.That(envelope.StreamId).IsEqualTo(stream);
        await Assert.That(envelope.TimestampUtc).IsEqualTo(timestamp);
        await Assert.That(envelope.Payload).IsNotNull();
    }
}

public sealed class GuidV7IdProviderTests
{
    [Test]
    public async Task NewId_returns_unique_sortable_guids()
    {
        var provider = new GuidV7IdProvider();
        var first = provider.NewId();
        var second = provider.NewId();
        await Assert.That(first).IsNotEqualTo(second);
        await Assert.That(first.Version).IsEqualTo(7);
    }
}
