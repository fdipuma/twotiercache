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

    private readonly LazyOperationTracker _lazyOperationTracker = new LazyOperationTracker();

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

    /// <inheritdoc/>
    public async ValueTask SetAsync<T>(string key, T value, TwoTierCacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        _memoryCache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpiration = options.AbsoluteExpiration });

        var serializedValue = Serialize(value, options);

        await _distributedCache.SetAsync(key, serializedValue,
            new DistributedCacheEntryOptions { AbsoluteExpiration = options.AbsoluteExpiration }, cancellationToken);

        EntrySet?.Invoke(this, new TwoTierCacheEntrySetEventArgs(key, value));
    }

    /// <inheritdoc/>
    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var result = await TryGetInternalAsync<T>(key, cancellationToken);

        return result.Success ? result.Value : default;
    }

    /// <inheritdoc/>
    public ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return TryGetInternalAsync<T>(key, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> GetOrCreateAsync<T>(string key, Func<TwoTierCacheEntryOptions, Task<T>> asyncValueFactory,
        CancellationToken cancellationToken = default)
    {
        var result = await TryGetInternalAsync<T>(key, cancellationToken);

        if (result.Success)
        {
            return result.Value;
        }

        try
        {
            return await _lazyOperationTracker.GetOrAddOperation(key, async cacheKey =>
            {
                var getResult = await TryGetInternalAsync<T>(key, CancellationToken.None);
                
                if (getResult.Success)
                {
                    return getResult.Value;
                }
                
                var entryOptions = new TwoTierCacheEntryOptions();
                
                var createdValue = await asyncValueFactory.Invoke(entryOptions).ConfigureAwait(false);
                
                await SetAsync(cacheKey, createdValue, entryOptions, CancellationToken.None);
                
                return createdValue;
            });
        }
        finally
        {
            _lazyOperationTracker.RemoveOperation(key);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        await _distributedCache.RemoveAsync(key, cancellationToken);
        EntryRemoved?.Invoke(this, new TwoTierCacheEntryRemovedEventArgs(key));
    }

    /// <inheritdoc/>
    public void EvictLocal(string key)
    {
        _memoryCache.Remove(key);
    }

    private async ValueTask<CacheResult<T>> TryGetInternalAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(key, out var result))
        {
            return CacheResult<T>.Found((T) result);
        }

        var bytes = await _distributedCache.GetAsync(key, cancellationToken);
        
        // IDistributedCache does not expose a way to understand if a key
        // was present or not in the cache, but we can try to infer this by the result
        // if the result is null, we infer that there was no key, while if an explicit null
        // was set on the distributed cache, the byte array value would not be null itself
        // but be a representation of the null value for the serializer

        if (bytes is null)
        {
            return CacheResult<T>.Miss;
        }

        var entry = Deserialize<T>(bytes);

        _memoryCache.Set(key, entry.Value,
            new MemoryCacheEntryOptions {AbsoluteExpiration = entry.Options?.AbsoluteExpiration});

        return CacheResult<T>.Found(entry.Value);
    }

    /// <summary>
    /// Serializes value and cache options with the best fitting serializer, if any.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The options to serialize</param>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <returns>Binary data of serialized value and options</returns>
    /// <exception cref="NotSupportedException">If no serializer is found that can serialize <see cref="T"/></exception>
    private byte[] Serialize<T>(T? value, TwoTierCacheEntryOptions options)
    {
        var serializer = _serializers.FirstOrDefault(s => s.CanSerialize(typeof(T)));

        if (serializer is null)
        {
            throw new NotSupportedException($"No serializer able to serialize {typeof(T).FullName} found");
        }

        return serializer.Serialize(new DistributedCacheEntry<T> { Value = value, Options = options });
    }

    /// <summary>
    /// Deserializes binary data into value and cache options with the best fitting serializer, if any.
    /// </summary>
    /// <param name="bytes">Binary serialized data.</param>
    /// <typeparam name="T">Type of the value</typeparam>
    /// <returns>A <see cref="DistributedCacheEntry{T}"/> containing the original value and options</returns>
    /// <exception cref="NotSupportedException">If no serializer is found that can serialize <see cref="T"/></exception>
    private DistributedCacheEntry<T> Deserialize<T>(byte[] bytes)
    {
        var serializer = _serializers.FirstOrDefault(s => s.CanSerialize(typeof(T)));

        if (serializer is null)
        {
            throw new NotSupportedException($"No serializer able to deserialize {typeof(T).FullName} found");
        }

        return serializer.Deserialize<T>(bytes);
    }

    /// <inheritdoc/>
    public event EventHandler<TwoTierCacheEntrySetEventArgs>? EntrySet;

    /// <inheritdoc/>
    public event EventHandler<TwoTierCacheEntryRemovedEventArgs>? EntryRemoved;
}
