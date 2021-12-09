using Microsoft.Extensions.DependencyInjection;
using TwoTierCache.Abstractions;

namespace TwoTierCache.EvictionSignaling.Redis;

public static class RedisCacheEvictionSignalerServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis cache eviction signaler services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="setupAction">An <see cref="Action{RedisCacheEvictionSignalerOptions}"/> to configure the provided
    /// <see cref="RedisCacheEvictionSignalerOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddRedisCacheEvictionSignaler(this IServiceCollection services,
        Action<RedisCacheEvictionSignalerOptions> setupAction)
    {
        services.AddOptions();
        services.Configure(setupAction);
        
        services.AddHostedService<RedisCacheEvictionSignaler>();

        return services;
    }
}
