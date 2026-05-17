using System.Collections;

namespace Novolis.Storage.Sqlite.Internals;

internal class TextTypes : IEnumerable<Type>
{
    public IEnumerator<Type> GetEnumerator()
    {
        yield return typeof(string);
        yield return typeof(Guid);
        yield return typeof(Guid?);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}