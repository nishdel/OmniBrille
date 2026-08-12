using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class BoundedLruCacheTests
{
    [Fact]
    public void GetOrAdd_ReusesValueAndRemainsBounded()
    {
        var cache = new BoundedLruCache<string, object>(2);
        var first = cache.GetOrAdd("one", _ => new object());

        Assert.Same(first, cache.GetOrAdd("one", _ => new object()));
        cache.GetOrAdd("two", _ => new object());
        cache.GetOrAdd("three", _ => new object());

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGetValue("one", out _));
    }

    [Fact]
    public void RecentAccess_ChangesEvictionOrder()
    {
        var cache = new BoundedLruCache<string, int>(2);
        cache.GetOrAdd("one", _ => 1);
        cache.GetOrAdd("two", _ => 2);
        Assert.True(cache.TryGetValue("one", out _));

        cache.GetOrAdd("three", _ => 3);

        Assert.True(cache.TryGetValue("one", out _));
        Assert.False(cache.TryGetValue("two", out _));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new BoundedLruCache<int, int>(2);
        cache.GetOrAdd(1, key => key);

        cache.Clear();

        Assert.Equal(0, cache.Count);
    }
}
