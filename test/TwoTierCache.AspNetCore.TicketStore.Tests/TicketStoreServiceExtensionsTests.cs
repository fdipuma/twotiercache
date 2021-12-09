using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.AspNetCore.TicketStore.Tests;

public class TicketStoreServiceExtensionsTests
{
    [Fact]
    public void Should_register_authentication_ticket_serializer_as_singleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTwoTiersCacheTicketStore("cookie1");

        // Assert
        services.Should().Contain(desc => desc.ImplementationType == typeof(DistributedCacheTicketSerializer) &&
                                          desc.ServiceType == typeof(IDistributedCacheEntrySerializer) &&
                                          desc.Lifetime == ServiceLifetime.Singleton);
    }
    
    [Fact]
    public void Should_register_session_storage_on_named_cookie_options()
    {
        // Arrange
        const string authenticationScheme = "cookie1";
        
        var services = new ServiceCollection();

        services.AddSingleton(Substitute.For<ITwoTierCache>());

        // Act
        services.AddTwoTiersCacheTicketStore(authenticationScheme);

        // Assert
        var provider = services.BuildServiceProvider();

        var optionsSnapshot = provider.GetRequiredService<IOptionsSnapshot<CookieAuthenticationOptions>>();

        optionsSnapshot.Should().NotBeNull();

        var options = optionsSnapshot.Get(authenticationScheme);

        options.Should().NotBeNull();

        options.SessionStore.Should().NotBeNull();
    }

    [Fact]
    public void Should_allow_chaining()
    {
        var services = new ServiceCollection();
        
        services.AddTwoTiersCacheTicketStore("").Should().BeSameAs(services);
    }
}
