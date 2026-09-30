using Novolis.Storage.Abstractions;
using Novolis.Storage.Abstractions.Events;

namespace Novolis.Storage.Tests.Abstractions;

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
