using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using NSubstitute;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.AspNetCore.TicketStore.Tests;

public class TwoTierCacheTicketStoreTests
{
    private readonly ITwoTierCache _cache;
    private readonly TwoTierCacheTicketStore _store;

    public TwoTierCacheTicketStoreTests()
    {
        _cache = Substitute.For<ITwoTierCache>();
        _store = new TwoTierCacheTicketStore(_cache);
    }

    [Theory]
    [MemberData(nameof(ExpirationData))]
    public async Task Should_store_ticket_in_cache_with_absolute_expiration_from_ticket(DateTimeOffset? expectedExpiration)
    {
        // Arrange
        var ticket = CreateAuthenticationTicket(expectedExpiration);

        // Act
        var key = await _store.StoreAsync(ticket);

        // Assert
        await _cache.Received()
            .SetAsync(
                key,
                ticket,
                Arg.Is<TwoTierCacheEntryOptions>(o => o.AbsoluteExpiration == expectedExpiration),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(ExpirationData))]
    public async Task Should_renew_ticket_in_cache_with_absolute_expiration_from_ticket(DateTimeOffset? expectedExpiration)
    {
        // Arrange
        var ticket = CreateAuthenticationTicket(expectedExpiration);

        var key = Guid.NewGuid().ToString();
        
        // Act
        await _store.RenewAsync(key, ticket);

        // Assert
        await _cache.Received()
            .SetAsync(
                key,
                ticket,
                Arg.Is<TwoTierCacheEntryOptions>(o => o.AbsoluteExpiration == expectedExpiration),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_retrieve_ticket_from_cache()
    {
        // Arrange
        var ticket = CreateAuthenticationTicket();

        var key = Guid.NewGuid().ToString();

        _cache.TryGetAsync<AuthenticationTicket>(key, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<CacheResult<AuthenticationTicket?>>(new CacheResult<AuthenticationTicket?>(ticket)));
        
        // Act
        var retrievedTicket = await _store.RetrieveAsync(key);

        // Assert
        retrievedTicket.Should().BeSameAs(ticket);
    }

    [Fact]
    public async Task Should_remove_ticket_from_cache()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();

        // Act
        await _store.RemoveAsync(key);

        // Assert
        await _cache.Received()
            .RemoveAsync(key, Arg.Any<CancellationToken>());
    }

    public static IEnumerable<object?[]> ExpirationData => new List<object?[]>
    {
        new object?[] { default(DateTimeOffset?) },
        new object?[] { new DateTimeOffset(2021, 1, 1, 10, 0, 0, TimeSpan.Zero) }
    };

    private static AuthenticationTicket CreateAuthenticationTicket(DateTimeOffset? expectedExpiration = default)
    {
        var identity = new ClaimsIdentity("test", "name", "role");
        identity.AddClaim(new Claim("name", "John Doe"));
        identity.AddClaim(new Claim("role", "User"));

        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationTicket(principal, "test")
        {
            Properties =
            {
                ExpiresUtc = expectedExpiration
            }
        };
    }
}
