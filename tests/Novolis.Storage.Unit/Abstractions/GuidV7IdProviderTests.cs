using Novolis.Storage.Abstractions;
using Novolis.Storage.Abstractions.Events;

namespace Novolis.Storage.Tests.Abstractions;

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
