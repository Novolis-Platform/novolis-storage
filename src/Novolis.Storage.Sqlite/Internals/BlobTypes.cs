using System.Collections;

namespace Novolis.Storage.Sqlite.Internals;

internal class BlobTypes : IEnumerable<Type>
{
    public IEnumerator<Type> GetEnumerator()
    {
        yield return typeof(byte[]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}