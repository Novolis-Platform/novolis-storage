using Microsoft.Extensions.DependencyInjection;
using Novolis.IO.Workspace;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Json;

/// <summary>
/// Registers the JSON directory provider: one folder per entity type, one file per entity (RootPath/TypeName/IdN.json).
/// </summary>
public static class JsonStorageExtensions
{
    /// <summary>Adds the JSON files repository provider.</summary>
    public static IStorageBuilder AddJsonProvider(
        this IStorageBuilder builder,
        Action<JsonFilesOptions> configure) =>
        AddJsonFilesProvider(builder, configure);

    /// <summary>Adds the JSON files repository provider.</summary>
    public static IStorageBuilder AddJsonFilesProvider(
        this IStorageBuilder builder,
        Action<JsonFilesOptions> configure)
    {
        var options = new JsonFilesOptions();
        configure(options);
        if (string.IsNullOrWhiteSpace(options.RootPath))
            options.RootPath = Path.Combine(Path.GetTempPath(), "novolis-storage-json");
        options.RootPath = Path.GetFullPath(options.RootPath);

        var workspace = new PhysicalFileWorkspace(options.RootPath);
        JsonFilesStartupValidator.Validate(options, workspace, out var processLock);

        builder.Services.AddKeyedSingleton<IFileWorkspace>(JsonStorageWorkspaceKey.Default, (_, _) => workspace);
        builder.Services.AddSingleton(options);
        if (processLock != null)
            builder.Services.AddSingleton(processLock);
        builder.Services.AddSingleton<IRepositoryProvider, JsonFilesRepositoryProvider>();
        builder.Services.AddTransient(typeof(IRepository<>), typeof(JsonFilesRepository<>));

        return builder;
    }
}
