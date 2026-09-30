using Novolis.Storage.Abstractions;
using Novolis.Storage.Abstractions.Events;

namespace Novolis.Storage.Tests.Abstractions;

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
