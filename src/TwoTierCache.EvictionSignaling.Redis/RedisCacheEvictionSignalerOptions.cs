using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace TwoTierCache.EvictionSignaling.Redis;

/// <summary>
/// Configuration options for <see cref="RedisCacheEvictionSignaler"/>.
/// </summary>
public class RedisCacheEvictionSignalerOptions : IOptions<RedisCacheEvictionSignalerOptions>
{
    /// <summary>
    /// The configuration used to connect to Redis.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// The configuration used to connect to Redis.
    /// This is preferred over Configuration.
    /// </summary>
    public ConfigurationOptions? ConfigurationOptions { get; set; }

    /// <summary>
    /// Gets or sets a delegate to create the ConnectionMultiplexer instance.
    /// </summary>
    public Func<Task<IConnectionMultiplexer>>? ConnectionMultiplexerFactory { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the Channel used for pub/sub of evicted keys
    /// </summary>
    public string EvictionChannelName { get; set; } = "Cache.Evictions";

    RedisCacheEvictionSignalerOptions IOptions<RedisCacheEvictionSignalerOptions>.Value => this;
}
