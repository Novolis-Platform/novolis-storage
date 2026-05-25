using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Storage.Abstractions;

/// <summary>
/// Fluent builder for configuring storage providers and type-specific bindings.
/// </summary>
public interface IStorageBuilder
{
    IServiceCollection Services { get; }
}
