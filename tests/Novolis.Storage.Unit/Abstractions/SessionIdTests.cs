using Novolis.Storage.Abstractions;
using Novolis.Storage.Abstractions.Events;

namespace Novolis.Storage.Tests.Abstractions;

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
