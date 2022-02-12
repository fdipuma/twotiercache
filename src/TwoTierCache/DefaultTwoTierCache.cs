using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using TwoTierCache.Abstractions;
using TwoTierCache.Abstractions.Events;

namespace TwoTierCache;

/// <summary>
/// Default implementation of a two-tier cache with <see cref="IMemoryCache"/> as first tier and
/// <see cref="IDistributedCache"/> as second tier.
/// </summary>
public class DefaultTwoTierCache : ITwoTierCache
{
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly IList<IDistributedCacheEntrySerializer> _serializers;

    public DefaultTwoTierCache(IDistributedCache distributedCache, IMemoryCache memoryCache,
        IEnumerable<IDistributedCacheEntrySerializer> serializers)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        var orderedSerializers = serializers.OrderByDescending(o => o.Priority).ToList();

        if (orderedSerializers.Count == 0)
        {
            throw new ArgumentException("At least one serializer must be provided", nameof(serializers));
        }

        _serializers = orderedSerializers;
    }

    public async ValueTask SetAsync<T>(string key, T value, TwoTierCacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        _memoryCache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpiration = options.AbsoluteExpiration });

        var serializedValue = Serialize(value, options);

        await _distributedCache.SetAsync(key, serializedValue,
            new DistributedCacheEntryOptions { AbsoluteExpiration = options.AbsoluteExpiration }, cancellationToken);

        EntrySet?.Invoke(this, new TwoTierCacheEntrySetEventArgs(key, value));
    }

    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out var result))
        {
            return (T)result;
        }

        var bytes = await _distributedCache.GetAsync(key, cancellationToken);

        if (bytes is null)
        {
            return default;
        }

        var entry = Deserialize<T>(bytes);

        _memoryCache.Set(key, entry.Value,
            new MemoryCacheEntryOptions { AbsoluteExpiration = entry.Options?.AbsoluteExpiration });

        return entry.Value;
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        await _distributedCache.RemoveAsync(key, cancellationToken);
        EntryRemoved?.Invoke(this, new TwoTierCacheEntryRemovedEventArgs(key));
    }

    public void EvictLocal(string key)
    {
        _memoryCache.Remove(key);
    }

    private byte[] Serialize<T>(T value, TwoTierCacheEntryOptions options)
    {
        var serializer = _serializers.FirstOrDefault(s => s.CanSerialize(typeof(T)));

        if (serializer is null)
        {
            throw new NotSupportedException($"No serializer able to serialize {typeof(T).FullName} found");
        }

        return serializer.Serialize(new DistributedCacheEntry<T> { Value = value, Options = options });
    }

    private DistributedCacheEntry<T> Deserialize<T>(byte[] bytes)
    {
        var serializer = _serializers.FirstOrDefault(s => s.CanSerialize(typeof(T)));

        if (serializer is null)
        {
            throw new NotSupportedException($"No serializer able to deserialize {typeof(T).FullName} found");
        }

        return serializer.Deserialize<T>(bytes);
    }

    public event EventHandler<TwoTierCacheEntrySetEventArgs>? EntrySet;
    public event EventHandler<TwoTierCacheEntryRemovedEventArgs>? EntryRemoved;
}
