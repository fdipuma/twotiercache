using TwoTierCache.Abstractions.Events;

namespace TwoTierCache.Abstractions;

/// <summary>
/// Represents a cache that uses a two-tier strategy, the fist tier is usually fast and volatile (e.g. memory) and the
/// second tier is usually distributed and persistent (e.g. a key-value storage database)
/// </summary>
public interface ITwoTierCache
{
    /// <summary>
    /// Create or overwrite an entry in all cache tiers.
    /// </summary>
    /// <param name="key">A string identifying the value.</param>
    /// <param name="value">The value to set in the cache.</param>
    /// <param name="options">The cache options for the value.</param>
    /// <param name="cancellationToken">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <typeparam name="T">The type of the value to set in the cache.</typeparam>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    ValueTask SetAsync<T>(string key, T value, TwoTierCacheEntryOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value with the given key from the cache.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="cancellationToken">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the located value or null.</returns>
    ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to get a value with the given key from the cache.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="cancellationToken">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing an <see cref="CacheResult{T}"/>.</returns>
    ValueTask<CacheResult<T>> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value with the given key from the cache and, if not found, creates an entry in all cache tiers using the specified value factory.
    /// </summary>
    /// <param name="key">A string identifying the value.</param>
    /// <param name="asyncValueFactory">The function which will be called if the value is not found in the cache.</param>
    /// <param name="cancellationToken">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <typeparam name="T">The type of the value to set in the cache.</typeparam>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    Task<T?> GetOrCreateAsync<T>(string key, Func<TwoTierCacheEntryOptions, Task<T>> asyncValueFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the value with the given key from all cache tiers.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    /// <param name="cancellationToken">Optional. The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
    /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts the value with the given key only from the in memory tier of the cache. This is used for managing remote eviction signals from
    /// different instances of the same cache.
    /// </summary>
    /// <param name="key">A string identifying the requested value.</param>
    void EvictLocal(string key);

    /// <summary>
    /// This event is raised every time an entry is set in the cache
    /// </summary>
    event EventHandler<TwoTierCacheEntrySetEventArgs>? EntrySet;
    
    /// <summary>
    /// This event is raised every time an entry is removed from the cache
    /// </summary>
    event EventHandler<TwoTierCacheEntryRemovedEventArgs>? EntryRemoved;
}
