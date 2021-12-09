using System.Collections.Concurrent;
using FluentAssertions;
using NSubstitute;
using TwoTierCache.Abstractions;
using TwoTierCache.Abstractions.Events;
using Xunit;

namespace TwoTierCache.EvictionSignaling.Redis.Tests;

public class RedisSignalerTests : RedisTestBase
{
    private readonly RedisCacheEvictionSignalerOptions _options;
    private readonly RedisCacheEvictionSignaler _signaler;
    private readonly ITwoTierCache _cache;

    public RedisSignalerTests()
    {
        _cache = Substitute.For<ITwoTierCache>();
        _options = new RedisCacheEvictionSignalerOptions { Configuration = RedisConnection };
        _signaler = new RedisCacheEvictionSignaler(_options, _cache);

        _signaler.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Should_publish_evicted_key_and_ignore_loopback_message_when_setting_new_entry()
    {
        // Arrange

        var key = Guid.NewGuid().ToString();

        var sub = Multiplexer.GetSubscriber();
        
        var tcs = new TaskCompletionSource();

        var notifiedKeys = new ConcurrentBag<string>();

        sub.Subscribe(_options.EvictionChannelName, (_, value) =>
        {
            notifiedKeys.Add(value);

            if (value == key)
            {
                tcs.SetResult();
            }
        });

        // Act
        _cache.EntrySet += Raise.EventWith(new TwoTierCacheEntrySetEventArgs(key, null));

        // Assert

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        notifiedKeys.Should().Contain(key);
        
        _cache.DidNotReceive()
            .EvictLocal(key);
    }

    [Fact]
    public async Task Should_publish_evicted_key_and_ignore_loopback_message_when_removing_entry()
    {
        // Arrange

        var key = Guid.NewGuid().ToString();

        var sub = Multiplexer.GetSubscriber();
        
        var tcs = new TaskCompletionSource();

        var notifiedKeys = new ConcurrentBag<string>();

        sub.Subscribe(_options.EvictionChannelName, (_, value) =>
        {
            notifiedKeys.Add(value);

            if (value == key)
            {
                tcs.SetResult();
            }
        });

        // Act
        _cache.EntryRemoved += Raise.EventWith(new TwoTierCacheEntryRemovedEventArgs(key));

        // Assert

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        notifiedKeys.Should().Contain(key);
        
        _cache.DidNotReceive()
            .EvictLocal(key);
    }

    [Fact]
    public async Task Should_receive_published_evicted_key()
    {
        // Arrange

        var key = Guid.NewGuid().ToString();

        var sub = Multiplexer.GetSubscriber();
        
        var tcs = new TaskCompletionSource();

        _cache
            .When(s => s.EvictLocal(key))
            .Do(_ => tcs.SetResult());

        // Act
        
        sub.Publish(_options.EvictionChannelName, key);

        // Assert

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _cache.Received(1)
            .EvictLocal(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _signaler.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            _signaler.Dispose();
        }
    }
}
