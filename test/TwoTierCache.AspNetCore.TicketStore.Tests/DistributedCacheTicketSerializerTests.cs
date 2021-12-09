using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.AspNetCore.TicketStore.Tests;

public class DistributedCacheTicketSerializerTests
{
    private readonly DistributedCacheTicketSerializer _sut;

    public DistributedCacheTicketSerializerTests()
    {
        _sut = new DistributedCacheTicketSerializer();
    }

    [Fact]
    public void Should_roundtrip_serialize_with_absolute_expiration()
    {
        // Arrange
        var expectedExpiration = new DateTimeOffset(2021, 1, 1, 10, 0, 0, TimeSpan.Zero);

        var ticket = CreateAuthenticationTicket(expectedExpiration);

        var entry = new DistributedCacheEntry<AuthenticationTicket> { Value = ticket };

        // Act
        var serialized = _sut.Serialize(entry);

        var deserialized = _sut.Deserialize<AuthenticationTicket>(serialized);

        // Assert
        serialized.Should().NotBeEmpty();

        deserialized.Should().NotBeNull();

        deserialized.Options.Should().NotBeNull();

        deserialized.Options!.AbsoluteExpiration.Should().Be(expectedExpiration);
    }

    [Fact]
    public void Should_throw_when_serializing_unsupported_type()
    {
        // Arrange
        var entry = new DistributedCacheEntry<CacheItem> { Value = new CacheItem() };

        // Act
        // Assert
        _sut.Invoking(s => s.Serialize(entry)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_throw_when_serializing_null()
    {
        // Arrange
        var entry = new DistributedCacheEntry<AuthenticationTicket> { Value = null };

        // Act
        // Assert
        _sut.Invoking(s => s.Serialize(entry)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Should_deserialize_to_null_when_data_is_empty()
    {
        // Arrange
        var data = Array.Empty<byte>();

        // Act
        var deserializedValue = _sut.Deserialize<CacheItem>(data);

        // Assert
        deserializedValue.Value.Should().BeNull();
    }

    [Fact]
    public void Should_deserialize_to_null_when_type_is_not_supported()
    {
        // Arrange
        var data = new byte[] { 12, 15, 16, 0, 5, 99, 12, 6 };

        // Act
        var deserializedValue = _sut.Deserialize<CacheItem>(data);

        // Assert
        deserializedValue.Value.Should().BeNull();
    }


    [Fact]
    public async Task Should_serialize_authentication_tickets_using_specific_serializer_when_using_default_cache()
    {
        // Arrange
        var ticket = CreateAuthenticationTicket(new DateTimeOffset(2021, 1, 1, 10, 0, 0, TimeSpan.Zero));

        var key = Guid.NewGuid().ToString();

        var services = new ServiceCollection();

        services.AddMemoryCache();
        services.AddDistributedMemoryCache();

        services.AddTwoTierCache();

        services.AddTwoTiersCacheTicketStore("cookie1");

        var provider = services.BuildServiceProvider();

        var concreteCache = provider.GetRequiredService<ITwoTierCache>();

        // Act
        await concreteCache.SetAsync(key, ticket, new TwoTierCacheEntryOptions());

        // Assert

        var distributedCache = provider.GetRequiredService<IDistributedCache>();

        var value = distributedCache.Get(key);

        var deserializedTicket = TicketSerializer.Default.Deserialize(value);

        deserializedTicket.Should().NotBeNull();

        deserializedTicket!.Properties.ExpiresUtc.Should().Be(ticket.Properties.ExpiresUtc);
    }

    private static AuthenticationTicket CreateAuthenticationTicket(DateTimeOffset? expectedExpiration = default)
    {
        var identity = new ClaimsIdentity("test", "name", "role");
        identity.AddClaim(new Claim("name", "John Doe"));
        identity.AddClaim(new Claim("role", "User"));

        var principal = new ClaimsPrincipal(identity);


        var ticket = new AuthenticationTicket(principal, "test") { Properties = { ExpiresUtc = expectedExpiration } };
        return ticket;
    }

    public class CacheItem
    {
        public string? Value { get; set; }
    }
}
