using System.Collections;

namespace Aggregator.IntegrationTests;

// The in-memory exporter adds from whichever thread ends a span; every access here takes the same lock.
public sealed class LockedCollection<T> : ICollection<T>
{
    private readonly List<T> items = [];

    public int Count
    {
        get
        {
            lock (items)
                return items.Count;
        }
    }

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        lock (items)
            items.Add(item);
    }

    public void Clear()
    {
        lock (items)
            items.Clear();
    }

    public bool Contains(T item)
    {
        lock (items)
            return items.Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (items)
            items.CopyTo(array, arrayIndex);
    }

    public bool Remove(T item)
    {
        lock (items)
            return items.Remove(item);
    }

    public List<T> Snapshot()
    {
        lock (items)
            return [.. items];
    }

    public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
