using Testcontainers.Azurite;

namespace Novolis.Storage.Unit.Repositories;

public static class AzuriteTableFixture
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static AzuriteContainer? _container;
    private static int _leases;

    public static bool IsContinuousIntegration { get; } =
        Truthy("CI") || Truthy("GITHUB_ACTIONS") || Truthy("TF_BUILD");

    public static bool IsAvailable { get; private set; }

    public static Exception? StartupError { get; private set; }

    public static string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("Azurite is not running.");

    private static string? _connectionString;

    public static async Task StartAsync()
    {
        if (IsContinuousIntegration)
            return;

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _leases++;
            if (IsAvailable)
                return;

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

            _leases--;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task StopAsync()
    {
        if (IsContinuousIntegration)
            return;

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_leases > 0)
                _leases--;
            if (_leases != 0)
                return;

        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }

        IsAvailable = false;
        _connectionString = null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool Truthy(string name) =>
        string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);
}
