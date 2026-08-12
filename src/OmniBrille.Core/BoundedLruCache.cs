namespace OmniBrille.Core;

/// <summary>
/// A small deterministic least-recently-used cache for UI hot-path resources.
/// It intentionally exposes no background work and never grows beyond its capacity.
/// </summary>
public sealed class BoundedLruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries;
    private readonly LinkedList<Entry> _usage = new();

    public BoundedLruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _entries = new Dictionary<TKey, LinkedListNode<Entry>>(capacity, comparer);
    }

    public int Capacity => _capacity;

    public int Count => _entries.Count;

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        _usage.Remove(node);
        _usage.AddFirst(node);
        value = node.Value.Value;
        return true;
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        if (TryGetValue(key, out var existing))
        {
            return existing;
        }

        var value = valueFactory(key);
        var node = _usage.AddFirst(new Entry(key, value));
        _entries.Add(key, node);
        if (_entries.Count > _capacity)
        {
            var oldest = _usage.Last!;
            _usage.RemoveLast();
            _entries.Remove(oldest.Value.Key);
        }

        return value;
    }

    public void Clear()
    {
        _entries.Clear();
        _usage.Clear();
    }

    private sealed record Entry(TKey Key, TValue Value);
}
