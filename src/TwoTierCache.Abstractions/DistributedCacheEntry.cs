namespace TwoTierCache.Abstractions;

/// <summary>
/// Represents an entry in the distributed tier of a <see cref="ITwoTierCache"/>
/// </summary>
/// <typeparam name="T">The cached value type</typeparam>
public class DistributedCacheEntry<T>
{
    /// <summary>
    /// Entry options
    /// </summary>
    public TwoTierCacheEntryOptions? Options { get; set; }
    /// <summary>
    /// Cached value
    /// </summary>
    public T? Value { get; set; }
}
