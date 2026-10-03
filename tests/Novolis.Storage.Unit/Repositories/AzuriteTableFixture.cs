using Testcontainers.Azurite;

namespace Novolis.Storage.Unit.Repositories;

public static class AzuriteTableFixture
{
    private static AzuriteContainer? _container;

    public static bool IsContinuousIntegration { get; } =
        Truthy("CI") || Truthy("GITHUB_ACTIONS") || Truthy("TF_BUILD");

    public static bool IsAvailable { get; private set; }

    public static Exception? StartupError { get; private set; }

    public static string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("Azurite is not running.");

    private static string? _connectionString;

    public static async Task StartAsync()
    {
        if (IsContinuousIntegration || IsAvailable)
            return;

        try
        {
            _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            StartupError = ex;
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }
    }

    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }

        IsAvailable = false;
        _connectionString = null;
    }

    private static bool Truthy(string name) =>
        string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);
}
