using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory;
using Novolis.Storage.Json;
using Novolis.Storage.Tests.Shared;
using System.Collections.Concurrent;

namespace Novolis.Storage.Tests.Abstractions;

public sealed class StorageServiceRegistrationTests
{
    [Test]
    public async Task Repository_factory_resolves_typed_repository()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddInMemoryProvider());
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var factory = host.Services.GetRequiredService<IRepositoryFactory>();
            var repository = factory.Create<ExampleClass>();
            await Assert.That(repository).IsNotNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task BindRepository_overrides_default_provider_for_type()
    {
        var jsonRoot = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<IInMemoryStore, TestInMemoryStore>();
        builder.Services.AddSingleton<InMemoryRepositoryProviderStub>();
        builder.Services.AddStorage(b =>
        {
            b.AddJsonProvider(o => o.RootPath = jsonRoot);
            b.BindRepository<ExampleClass, InMemoryRepositoryProviderStub>();
        });

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "bound" };
            await repository.UpsertAsync(entity);
            var loaded = await repository.TryGetAsync(entity.Id);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.SomeData).IsEqualTo("bound");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private sealed class TestInMemoryStore : IInMemoryStore
    {
        private readonly ConcurrentDictionary<Type, object> _dicts = new();

        public ConcurrentDictionary<Guid, T> GetOrAddDictionary<T>() where T : class, IHasId =>
            (ConcurrentDictionary<Guid, T>)_dicts.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<Guid, T>());
    }

    private sealed class InMemoryRepositoryProviderStub(IInMemoryStore store) : IRepositoryProvider
    {
        public IRepository<T> Create<T>() where T : class, IHasId =>
            new InMemoryRepositoryStub<T>(store);
    }

    private sealed class InMemoryRepositoryStub<T>(IInMemoryStore store) : IRepository<T> where T : class, IHasId
    {
        private readonly ConcurrentDictionary<Guid, T> _dict = store.GetOrAddDictionary<T>();

        public IEnumerable<T> All() => _dict.Values;

        public ValueTask<T?> TryGetAsync(Guid id, CancellationToken ct = default)
        {
            _dict.TryGetValue(id, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask UpsertAsync(T entity, CancellationToken ct = default)
        {
            _dict[entity.Id] = entity;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
            ValueTask.FromResult(_dict.TryRemove(id, out _));
    }
}
