using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TwoTierCache.Abstractions;

namespace TwoTierCache.AspNetCore.TicketStore;

/// <summary>
/// <see cref="AuthenticationTicket"/> store that uses a <see cref="ITwoTierCache"/> as backplane for
/// storing and retrieving auth info
/// </summary>
public class TwoTierCacheTicketStore : ITicketStore
{
    private const string KeyPrefix = "AuthSessionStore-";
    private readonly ITwoTierCache _cache;
    
    public TwoTierCacheTicketStore(ITwoTierCache cache)
    {
        _cache = cache;
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, default);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken )
    {
        var key = KeyPrefix + Guid.NewGuid();
        await SetAsync(key, ticket, cancellationToken);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, default);

    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken )
    {
        await SetAsync(key, ticket, cancellationToken);
    }

#pragma warning disable CS8613
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, default);
#pragma warning restore CS8613
    
    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        var result = await _cache.TryGetAsync<AuthenticationTicket>(key, cancellationToken);
        return result.Success ? result.Value : null;
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, default);

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }

    private async Task SetAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken )
    {
        var options = new TwoTierCacheEntryOptions
        {
            AbsoluteExpiration = ticket.Properties.ExpiresUtc
        };

        await _cache.SetAsync(key, ticket, options, cancellationToken);
    }
}
