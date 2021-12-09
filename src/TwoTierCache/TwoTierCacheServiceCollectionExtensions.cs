using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TwoTierCache.Abstractions;

namespace TwoTierCache;

public static class TwoTierCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default implementation of <see cref="ITwoTierCache"/> (wich uses <see cref="IMemoryCache"/> and
    /// <see cref="IDistributedCache"/>) along with <see cref="DistributedCacheEntryJsonSerializer"/>
    /// to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddTwoTierCache(this IServiceCollection services)
    {
        services.AddTransient<IDistributedCacheEntrySerializer, DistributedCacheEntryJsonSerializer>();
        services.AddSingleton<ITwoTierCache, DefaultTwoTierCache>();

        return services;
    }
}
