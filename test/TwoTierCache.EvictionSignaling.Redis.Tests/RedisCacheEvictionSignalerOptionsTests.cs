using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.EvictionSignaling.Redis.Tests;

public class RedisCacheEvictionSignalerOptionsTests : RedisTestBase
{
    [Fact]
    public async Task Should_connect_from_configuration_string_correctly()
    {
        var options = new RedisCacheEvictionSignalerOptions { Configuration = RedisConnection };

        await TestConnectionAsync(options);
    }
    
    [Fact]
    public async Task Should_use_custom_channel_name()
    {
        var options = new RedisCacheEvictionSignalerOptions
        {
            Configuration = RedisConnection,
            EvictionChannelName = "MyCustomChannelName"
        };

        await TestConnectionAsync(options);
    }

    [Fact]
    public async Task Should_connect_from_configuration_options_correctly()
    {
        var options = new RedisCacheEvictionSignalerOptions
        {
            ConfigurationOptions = new ConfigurationOptions { EndPoints = { RedisConnection } }
        };

        await TestConnectionAsync(options);
    }

    [Fact]
    public async Task Should_connect_from_connection_multiplexer_factory_correctly()
    {
        var options = new RedisCacheEvictionSignalerOptions
        {
            ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(Multiplexer)
        };

        await TestConnectionAsync(options);
    }

    private async Task TestConnectionAsync(RedisCacheEvictionSignalerOptions options)
    {
        // Arrange
        var key = Guid.NewGuid().ToString();

        var cache = Substitute.For<ITwoTierCache>();
        
        using var signaler = new RedisCacheEvictionSignaler(options, cache);

        // Act

        await signaler.StartAsync(CancellationToken.None);

        var sub = Multiplexer.GetSubscriber();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        sub.Subscribe(options.EvictionChannelName, (channel, value) =>
        {
            if (value == key)  tcs.TrySetResult();
        });
        
        sub.Publish(options.EvictionChannelName, key);

        // Assert

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(100);

        cache.Received().EvictLocal(key);

        await signaler.StopAsync(CancellationToken.None);
    }
}
