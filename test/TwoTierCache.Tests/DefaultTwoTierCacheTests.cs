using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using NSubstitute;
using TwoTierCache.Abstractions;
using TwoTierCache.Abstractions.Events;
using Xunit;

namespace TwoTierCache.Tests;

public class DefaultTwoTierCacheTests
{
    private readonly ISystemClock _systemClock;
    private readonly MemoryCache _memoryCache;
    private readonly MemoryDistributedCache _distributedCache;
    private readonly DistributedCacheEntryJsonSerializer _serializer;
    private readonly DefaultTwoTierCache _sut;

    public DefaultTwoTierCacheTests()
    {
        _systemClock = Substitute.For<ISystemClock>();

        _memoryCache = new MemoryCache(new MemoryCacheOptions { Clock = _systemClock });

        _distributedCache = new MemoryDistributedCache(
            new OptionsWrapper<MemoryDistributedCacheOptions>(
                new MemoryDistributedCacheOptions { Clock = _systemClock })
        );

        _serializer = new DistributedCacheEntryJsonSerializer();

        _sut = new DefaultTwoTierCache(_distributedCache, _memoryCache, new[] { _serializer });
    }

    [Fact]
    public void Should_throw_argument_exception_if_serializers_are_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            new DefaultTwoTierCache(_distributedCache, _memoryCache, Array.Empty<IDistributedCacheEntrySerializer>()));
    }

    [Fact]
    public async Task Should_get_default_when_both_are_empty()
    {
        var key = GetRandomKey();

        var result = await _sut.TryGetAsync<CacheItem>(key);

        result.Success.Should().BeFalse();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Should_get_from_distributed_when_local_is_empty()
    {
        var key = GetRandomKey();

        var content = _serializer.Serialize(new DistributedCacheEntry<CacheItem>
        {
            Value = new CacheItem { InnerValue = "hello" }
        });

        _memoryCache.Remove(key);
        _distributedCache.Set(key, content);

        var result = await _sut.TryGetAsync<CacheItem>(key);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.InnerValue.Should().Be("hello");
    }

    [Fact]
    public async Task Should_get_default_from_distributed_when_local_is_empty_and_content_is_null_literal()
    {
        var key = GetRandomKey();

        var content = Encoding.UTF8.GetBytes("null");

        _memoryCache.Remove(key);
        _distributedCache.Set(key, content);

        var result = await _sut.TryGetAsync<CacheItem>(key);

        result.Success.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Should_get_null_from_distributed_when_cache_miss()
    {
        var key = GetRandomKey();

        var content = Encoding.UTF8.GetBytes("null");

        _memoryCache.Remove(key);
        _distributedCache.Remove(key);

        var result = await _sut.GetAsync<CacheItem>(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_get_null_from_distributed_when_cache_entry_is_null()
    {
        var key = GetRandomKey();

        var content = Encoding.UTF8.GetBytes("null");

        _memoryCache.Set(key, (CacheItem?)null);
        _distributedCache.Set(key, content);
        
        var result = await _sut.GetAsync<CacheItem>(key);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_local_when_is_empty_but_distributed_has_value()
    {
        var key = GetRandomKey();

        var content = _serializer.Serialize(new DistributedCacheEntry<CacheItem>
        {
            Value = new CacheItem { InnerValue = "hello" }
        });

        _memoryCache.Remove(key);
        _distributedCache.Set(key, content);

        _ = await _sut.TryGetAsync<CacheItem>(key);

        _memoryCache.Get(key).Should().NotBeNull();
    }

    [Fact]
    public async Task Should_get_from_local_when_key_exists()
    {
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        _memoryCache.Set(key, content);

        _ = await _sut.TryGetAsync<CacheItem>(key);

        var result = _memoryCache.Get<CacheItem>(key);

        result.Should().NotBeNull();
        result!.InnerValue.Should().Be(content.InnerValue);
    }

    [Fact]
    public async Task Should_set_into_distributed_when_setting_new_item()
    {
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        await _sut.SetAsync(key, content, new TwoTierCacheEntryOptions());

        var distributedValue = _distributedCache.Get(key);

        distributedValue.Should().NotBeNull();

        var entry = _serializer.Deserialize<CacheItem>(distributedValue);

        entry.Should().NotBeNull();

        entry!.Value!.InnerValue.Should().Be(content.InnerValue);
    }

    [Fact]
    public async Task Should_set_null_into_distributed_cache()
    {
        var key = GetRandomKey();

        CacheItem? content = null;

        await _sut.SetAsync(key, content, new TwoTierCacheEntryOptions());

        var distributedValue = _distributedCache.Get(key);

        distributedValue.Should().NotBeNull();

        var item = _serializer.Deserialize<CacheItem>(distributedValue);

        item.Should().NotBeNull();
        item!.Value.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_null_into_memory_cache()
    {
        var key = GetRandomKey();

        CacheItem? content = null;

        await _sut.SetAsync(key, content, new TwoTierCacheEntryOptions());

        var valueRetrieved = _memoryCache.TryGetValue(key, out var actualContent);

        valueRetrieved.Should().BeTrue();
        actualContent.Should().BeNull();
    }

    [Fact]
    public async Task Should_set_into_memory_when_setting_new_item()
    {
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        await _sut.SetAsync(key, content, new TwoTierCacheEntryOptions());

        var item = _memoryCache.Get(key) as CacheItem;

        item.Should().NotBeNull();
        item!.InnerValue.Should().Be(content.InnerValue);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public async Task Should_set_correct_absolute_expiration_in_memory_when_setting_new_item(int expiresInSeconds,
        bool expectedToBeNull)
    {
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        var currentDate = new DateTimeOffset(2021, 1, 1, 10, 0, 0, TimeSpan.Zero);

        _systemClock.UtcNow.Returns(currentDate);

        await _sut.SetAsync(key, content,
            new TwoTierCacheEntryOptions { AbsoluteExpiration = currentDate.AddSeconds(expiresInSeconds) });

        var value = _memoryCache.Get(key);

        if (expectedToBeNull)
        {
            value.Should().BeNull();
        }
        else
        {
            value.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public async Task Should_set_correct_absolute_expiration_in_distributed_when_setting_new_item(int expiresInSeconds,
        bool expectedToBeNull)
    {
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        var currentDate = new DateTimeOffset(2021, 1, 1, 10, 0, 0, TimeSpan.Zero);

        _systemClock.UtcNow.Returns(currentDate);

        await _sut.SetAsync(key, content,
            new TwoTierCacheEntryOptions { AbsoluteExpiration = currentDate.AddSeconds(expiresInSeconds) });

        var value = _distributedCache.Get(key);

        if (expectedToBeNull)
        {
            value.Should().BeNull();
        }
        else
        {
            value.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Should_remove_from_memory_when_removing_key()
    {
        var key = GetRandomKey();

        _memoryCache.Set(key, new CacheItem { InnerValue = "hello world" });

        await _sut.RemoveAsync(key);

        _memoryCache.Get(key).Should().BeNull();
    }

    [Fact]
    public async Task Should_remove_from_distributed_when_removing_key()
    {
        var key = GetRandomKey();

        _distributedCache.Set(key, Array.Empty<byte>());

        await _sut.RemoveAsync(key);

        _distributedCache.Get(key).Should().BeNull();
    }

    [Fact]
    public async Task Should_signal_when_setting_new_key()
    {
        // Arrange
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        string? evictedKey = null;

        void Handler(object? sender, TwoTierCacheEntrySetEventArgs args) => evictedKey = args.Key;

        _sut.EntrySet += Handler;
        
        // Act
        await _sut.SetAsync(key, content, new TwoTierCacheEntryOptions());

        // Assert
        evictedKey.Should().Be(key);
        
        _sut.EntrySet -= Handler;
    }

    [Fact]
    public async Task Should_signal_when_removing_a_key()
    {
        // Arrange
        var key = GetRandomKey();

        string? evictedKey = null;

        void Handler(object? sender, TwoTierCacheEntryRemovedEventArgs args) => evictedKey = args.Key;

        _sut.EntryRemoved += Handler;
        
        // Act
        await _sut.RemoveAsync(key);

        // Assert
        evictedKey.Should().Be(key);
        
        _sut.EntryRemoved -= Handler;
    }

    [Fact]
    public void Should_evict_from_memory_when_signaling_a_new_key_to_evict()
    {
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        _memoryCache.Set(key, content);

        _sut.EvictLocal(key);

        _memoryCache.TryGetValue(key, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Should_throw_when_serializer_does_not_support_type()
    {
        // Arrange
        var key = GetRandomKey();

        var unsupportedSerializer = Substitute.For<IDistributedCacheEntrySerializer>();
        unsupportedSerializer.CanSerialize(typeof(void)).ReturnsForAnyArgs(false);

        var sut = new DefaultTwoTierCache(_distributedCache, _memoryCache, new[] { unsupportedSerializer });
        
        var content = new CacheItem { InnerValue = "hello world" };

        // Act/Assert
        await sut.Awaiting(s => s.SetAsync(key, content, new TwoTierCacheEntryOptions(), default))
            .Should()
            .ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Should_throw_when_deserializer_does_not_support_type()
    {
        // Arrange
        
        var key = GetRandomKey();

        var content = new CacheItem { InnerValue = "hello world" };

        var value = _serializer.Serialize(new DistributedCacheEntry<CacheItem> { Value = content });

        _distributedCache.Set(key, value);

        var unsupportedSerializer = Substitute.For<IDistributedCacheEntrySerializer>();
        unsupportedSerializer.CanSerialize(typeof(void)).ReturnsForAnyArgs(false);

        var sut = new DefaultTwoTierCache(_distributedCache, _memoryCache, new[] { unsupportedSerializer });
        
        // Act/Assert
        
        await sut.Awaiting(s => s.TryGetAsync<CacheItem>(key, default))
            .Should()
            .ThrowExactlyAsync<NotSupportedException>();
    }
    
    [Fact]
    public async Task Should_call_value_factory_only_once_when_GetOrCreateAsync_called_from_multiple_threads()
    {
        const string key = "cachekey";

        var valueFactoryCallCount = 0;

        async Task<CacheItem> AsyncValueFactory(TwoTierCacheEntryOptions options)
        {
            Interlocked.Add(ref valueFactoryCallCount, 1);
            await Task.Delay(100);
            return new CacheItem { InnerValue = "inner" };
        }

        var results = await Task.WhenAll(
            Task.Run(() => _sut.GetOrCreateAsync(key, AsyncValueFactory)),
            Task.Run(() => _sut.GetOrCreateAsync(key, AsyncValueFactory)),
            Task.Run(() => _sut.GetOrCreateAsync(key, AsyncValueFactory))
        );

        valueFactoryCallCount.Should().Be(1);
        results.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_return_from_cache_when_GetOrCreateAsync_called_and_get_returns_a_value()
    {
        const string key = "cachekey";
        
        var valueFactoryCallCount = 0;

        _memoryCache.Set(key, new CacheItem { InnerValue = "cached" });

        async Task<CacheItem> AsyncValueFactory(TwoTierCacheEntryOptions options)
        {
            Interlocked.Add(ref valueFactoryCallCount, 1);
            await Task.Delay(100);
            return new CacheItem { InnerValue = "inner" };
        }

        var results = await Task.WhenAll(
            Task.Run(() => _sut.GetOrCreateAsync(key, AsyncValueFactory)),
            Task.Run(() => _sut.GetOrCreateAsync(key, AsyncValueFactory)),
            Task.Run(() => _sut.GetOrCreateAsync(key, AsyncValueFactory))
        );

        valueFactoryCallCount.Should().Be(0);
        results.Distinct().Should().HaveCount(1);
    }

    private static string GetRandomKey()
    {
        return Guid.NewGuid().ToString();
    }
}

public class CacheItem
{
    public string? InnerValue { get; set; }
}
