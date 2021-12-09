using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.Tests;

public class CacheServiceExtensionsTests
{
    [Fact]
    public void Should_register_TwoTierCache_as_singleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTwoTierCache();

        // Assert
        var twoTierCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(ITwoTierCache));

        twoTierCache.Should().NotBeNull();
        twoTierCache!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
    
    [Fact]
    public void Should_register_distributed_cache_entry_serializer_as_transient_if_no_implementation_is_already_registered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTwoTierCache();

        // Assert
        var twoTierCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCacheEntrySerializer));

        twoTierCache.Should().NotBeNull();
        twoTierCache!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }
    
    [Fact]
    public void Should_not_register_distributed_cache_entry_serializer_if_another_implementation_is_already_registered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDistributedCacheEntrySerializer>());

        // Act
        services.AddTwoTierCache();

        // Assert
        services.Should().ContainSingle(desc => desc.ServiceType == typeof(IDistributedCacheEntrySerializer))
            .Which
            .Should()
            .Match<ServiceDescriptor>(e => e.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void Should_replace_already_registered_TwoTierCache()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDistributedCache>());
        services.AddSingleton(Substitute.For<IMemoryCache>());
        services.AddScoped(sp => Substitute.For<ITwoTierCache>());

        // Act
        services.AddTwoTierCache();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var twoTierCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(ITwoTierCache));

        twoTierCache.Should().NotBeNull();
        twoTierCache!.Lifetime.Should().Be(ServiceLifetime.Scoped);

        serviceProvider.GetRequiredService<ITwoTierCache>().Should().BeOfType<DefaultTwoTierCache>();
    }

    [Fact]
    public void Should_allow_chaining()
    {
        var services = new ServiceCollection();
        
        services.AddTwoTierCache().Should().BeSameAs(services);
    }
}
