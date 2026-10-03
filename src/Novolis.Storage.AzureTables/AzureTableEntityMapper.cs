using System.Reflection;
using Azure.Data.Tables;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureTables;

internal static class AzureTableEntityMapper
{
    public static TableEntity ToTable<T>(T entity) where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(entity);
        AzureTableValues.EnsureMappable(typeof(T));
        var row = new TableEntity(AzureTableKeys.Partition, AzureTableKeys.RowKey(entity.Id));
        foreach (var property in Cache<T>.Properties)
        {
            var value = property.GetValue(entity);
            if (value is null)
                continue;

            row[property.Name] = AzureTableValues.Normalize(property.PropertyType, value);
        }

        return row;
    }

    public static T ToObject<T>(TableEntity row) where T : class, IHasId
    {
        AzureTableValues.EnsureMappable(typeof(T));
        var entity = Activator.CreateInstance<T>()
            ?? throw new InvalidOperationException($"Could not create an instance of {typeof(T).Name}.");
        if (Cache<T>.Id is null || !Cache<T>.Id.CanWrite)
            throw new InvalidOperationException($"{typeof(T).Name} must have a writable Id.");

        Cache<T>.Id.SetValue(entity, Guid.Parse(row.RowKey));
        foreach (var property in Cache<T>.Properties)
        {
            if (!row.TryGetValue(property.Name, out var raw) || raw is null)
                continue;

            property.SetValue(entity, AzureTableValues.Read(property.PropertyType, raw));
        }

        return entity;
    }

    private static class Cache<T> where T : class, IHasId
    {
        public static readonly PropertyInfo? Id = typeof(T).GetProperty(nameof(IHasId.Id));

        public static readonly PropertyInfo[] Properties = typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => !AzureTableValues.IsReserved(property))
            .Where(AzureTableValues.IsStoredProperty)
            .ToArray();
    }
}
