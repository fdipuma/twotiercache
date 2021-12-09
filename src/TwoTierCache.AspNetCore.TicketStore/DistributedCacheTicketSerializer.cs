using Microsoft.AspNetCore.Authentication;
using TwoTierCache.Abstractions;

namespace TwoTierCache.AspNetCore.TicketStore;

/// <summary>
/// Custom serializer that handles <see cref="AuthenticationTicket"/> entires using the default
/// <see cref="TicketSerializer"/>
/// </summary>
public class DistributedCacheTicketSerializer : IDistributedCacheEntrySerializer
{
    public bool CanSerialize(Type type) => type == typeof(AuthenticationTicket);

    public int Priority => 1000;

    public byte[] Serialize<T>(DistributedCacheEntry<T> entry)
    {
        if (entry.Value is not AuthenticationTicket ticket)
        {
            throw new InvalidOperationException("Unsupported entry type");
        }

        return TicketSerializer.Default.Serialize(ticket);
    }

    public DistributedCacheEntry<T> Deserialize<T>(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return new DistributedCacheEntry<T>();
        }
        
        var ticket = TicketSerializer.Default.Deserialize(bytes);

        if (ticket is not T value)
        {
            value = default!;
        }

        return new DistributedCacheEntry<T>
        {
            Options = new TwoTierCacheEntryOptions
            {
                AbsoluteExpiration = ticket?.Properties.ExpiresUtc
            },
            Value = value
        };
    }
}
