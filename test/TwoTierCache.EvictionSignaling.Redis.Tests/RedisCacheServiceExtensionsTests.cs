using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.EvictionSignaling.Redis.Tests;

public class RedisCacheServiceExtensionsTests
{
    [Fact]
    public void Should_register_signaler_as_singleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRedisCacheEvictionSignaler(_ => { });

        // Assert
        var signaler = services.FirstOrDefault(desc => desc.ImplementationType == typeof(RedisCacheEvictionSignaler));

        signaler.Should().NotBeNull();
        signaler!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Should_register_hosted_service_as_singleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRedisCacheEvictionSignaler(_ => { });

        // Assert
        var signaler = services.FirstOrDefault(desc => desc.ServiceType == typeof(IHostedService));

        signaler.Should().NotBeNull();
        signaler!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
    
    [Fact]
    public void Should_allow_chaining()
    {
        var services = new ServiceCollection();

        services.AddRedisCacheEvictionSignaler(_ => { }).Should().BeSameAs(services);
    }
}
