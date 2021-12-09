using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TwoTierCache.Abstractions;

namespace TwoTierCache.AspNetCore.TicketStore;

public static class TwoTierCacheTicketStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds services for storing and retrieving <see cref="AuthenticationTicket"/> from a
    /// Two Tier Cache to <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="authenticationScheme">The authentication scheme name used when configuring <see cref="CookieAuthenticationOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddTwoTiersCacheTicketStore(this IServiceCollection services,
        string authenticationScheme)
    {
        services.AddSingleton<IDistributedCacheEntrySerializer, DistributedCacheTicketSerializer>();
        services.AddSingleton<TwoTierCacheTicketStore>();

        services
            .AddOptions<CookieAuthenticationOptions>(authenticationScheme)
            .Configure<TwoTierCacheTicketStore>((options, store) => options.SessionStore = store);

        return services;
    }
}
