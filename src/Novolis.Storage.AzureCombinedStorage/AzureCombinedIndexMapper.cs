using Azure.Data.Tables;
using Novolis.Storage.Indexing;

namespace Novolis.Storage.AzureCombinedStorage;

internal static class AzureCombinedIndexMapper
{
    public static IEnumerable<TableEntity> ToEntities<T>(
        T entity,
        IndexDescriptor descriptor,
        Guid id,
        Guid revision)
        where T : class
    {
        var values = descriptor.Properties
            .Select(property => IndexValueAccessor.Read(entity, property))
            .ToArray();
        if (values.Any(value => value.Count == 0))
            yield break;

        foreach (var combination in Combinations(values, 0, []))
        {
            var encoded = string.Join(
                "|",
                combination.Select(IndexKeyEncoder.Encode));
            var identity = descriptor.Identity + "|" + descriptor.Version;
            var row = new TableEntity(
                AzureCombinedKeys.IndexPartition(identity),
                AzureCombinedKeys.IndexRow(encoded, id, revision))
            {
                ["IndexIdentity"] = descriptor.Identity,
                ["IndexVersion"] = descriptor.Version,
                ["EntityId"] = id,
                ["Revision"] = revision,
                ["EncodedValue"] = encoded,
            };
            yield return row;
        }
    }

    private static IEnumerable<IReadOnlyList<object?>> Combinations(
        IReadOnlyList<IReadOnlyList<object?>> values,
        int index,
        IReadOnlyList<object?> current)
    {
        if (index == values.Count)
        {
            yield return current.ToArray();
            yield break;
        }

        foreach (var value in values[index])
            foreach (var combination in Combinations(values, index + 1, current.Append(value).ToArray()))
                yield return combination;
    }
}
