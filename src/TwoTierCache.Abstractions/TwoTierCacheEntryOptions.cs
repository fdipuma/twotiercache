namespace TwoTierCache.Abstractions;

/// <summary>
/// Options for storing an entry into a <see cref="ITwoTierCache"/>
/// </summary>
public class TwoTierCacheEntryOptions
{
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}
