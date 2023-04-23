using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using TwoTierCache.Abstractions;
using Xunit;

namespace TwoTierCache.Tests;

public class CacheSampedePreventionTests
{
    [Fact]
    public async Task Should_call_async_value_provider_only_once()
    {
        var cache = CreateDefaultInMemoryTwoTierCache();

        var number = 0;

        var databaseCompleted = new TaskCompletionSource();

        async Task<CacheItem> AsyncValueProvider(TwoTierCacheEntryOptions options)
        {
            Interlocked.Increment(ref number);
            await databaseCompleted.Task;
            options.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(2);
            return new CacheItem {Number = number};
        }

        var tasks = await SpinTasksAndWaitForStartAsync(
            () => cache.GetOrCreateAsync("key1", AsyncValueProvider)
        );

        databaseCompleted.SetResult();
        
        var results = await Task.WhenAll(tasks);

        number.Should().Be(1);
        results.Should()
            .NotContainNulls()
            .And
            .OnlyContain(i => i!.Number == 1).And.HaveCount(10);
    }

    [Fact]
    public async Task Should_get_from_distributed_cache_only_once()
    {
        var distributedCacheMock = Substitute.For<IDistributedCache>();

        distributedCacheMock
            .GetAsync("", CancellationToken.None)
            .ReturnsForAnyArgs(new DistributedCacheEntryJsonSerializer().Serialize(new DistributedCacheEntry<CacheItem>()
            {
                Value = new CacheItem {Number = 99},
                Options = new TwoTierCacheEntryOptions {AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)}
            }));

        var cache = CreateDefaultInMemoryTwoTierCache(distributedCacheMock);

        var number = 0;

        var databaseCompleted = new TaskCompletionSource();

        async Task<CacheItem> AsyncValueProvider(TwoTierCacheEntryOptions options)
        {
            Interlocked.Increment(ref number);
            await databaseCompleted.Task;
            options.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(2);
            return new CacheItem {Number = number};
        }

        var tasks = await SpinTasksAndWaitForStartAsync(
            () => cache.GetOrCreateAsync("key1", AsyncValueProvider)
        );

        databaseCompleted.SetResult();
        
        var results = await Task.WhenAll(tasks);

        number.Should().Be(0);
        results.Should()
            .NotContainNulls()
            .And
            .OnlyContain(i => i!.Number == 99).And.HaveCount(10);
        await distributedCacheMock.ReceivedWithAnyArgs(1).GetAsync("", CancellationToken.None);
    }

    [Fact]
    public async Task Should_propagate_exception_from_async_value_provider()
    {
        var cache = CreateDefaultInMemoryTwoTierCache();

        var number = 0;
        
        var databaseCompleted = new TaskCompletionSource();

#pragma warning disable CS1998
        async Task<CacheItem> AsyncValueProvider(TwoTierCacheEntryOptions options)
#pragma warning restore CS1998
        {
            Interlocked.Increment(ref number);
            await databaseCompleted.Task;
            options.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(2);
            throw new InvalidOperationException("my exception");
        }

        var tasks = await SpinTasksAndWaitForStartAsync(
            () => cache.GetOrCreateAsync("key1", AsyncValueProvider)
        );

        databaseCompleted.SetResult();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception)
        {
            UnwrapExceptions(tasks).Should().ContainItemsAssignableTo<InvalidOperationException>().And.HaveCount(10);
            number.Should().Be(1);
            
            return;
        }

        throw new AssertionFailedException("should have thrown");
    }

    [Fact]
    public async Task Should_propagate_exception_from_distributed_cache()
    {
        var distributedCacheMock = Substitute.For<IDistributedCache>();

        distributedCacheMock
            .GetAsync("", CancellationToken.None)
            .ReturnsForAnyArgs(_ =>
            {
                // simulate distributed cache failing after 2000 ms
                return Task.Delay(2000)
                    .ContinueWith<byte[]?>(_ => throw new InvalidOperationException("my exception"));
            });

        var cache = CreateDefaultInMemoryTwoTierCache(distributedCacheMock);

        var number = 0;

        var databaseCompleted = new TaskCompletionSource();

        async Task<CacheItem> AsyncValueProvider(TwoTierCacheEntryOptions options)
        {
            Interlocked.Increment(ref number);
            await databaseCompleted.Task;
            options.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(2);
            return new CacheItem {Number = number};
        }
        
        var tasks = await SpinTasksAndWaitForStartAsync(
            () => cache.GetOrCreateAsync("key1", AsyncValueProvider)
        );

        databaseCompleted.SetResult();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception)
        {
            UnwrapExceptions(tasks).Should().ContainItemsAssignableTo<InvalidOperationException>().And.HaveCount(10);
            number.Should().Be(0);
            await distributedCacheMock.ReceivedWithAnyArgs(1).GetAsync("", CancellationToken.None);
            
            return;
        }

        throw new AssertionFailedException("should have thrown");
    }

    private static DefaultTwoTierCache CreateDefaultInMemoryTwoTierCache(IDistributedCache? distributedCache = null)
    {
        var memoryCache = new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions()));

        distributedCache ??=
            new MemoryDistributedCache(
                new OptionsWrapper<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions()));

        return new DefaultTwoTierCache(distributedCache, memoryCache,
            new[] {new DistributedCacheEntryJsonSerializer()});
    }

    private static async Task<Task<T>[]> SpinTasksAndWaitForStartAsync<T>(Func<Task<T>> func)
    {
        var taskStartCompletions = Enumerable.Range(0, 10)
            .Select(_ => new TaskCompletionSource())
            .ToArray();

        var tasks = Enumerable.Range(0, 10)
            .Select(async i =>
            {
                taskStartCompletions[i].SetResult();
                return await func();
            })
            .ToArray();

        await Task.WhenAll(taskStartCompletions.Select(tcs => tcs.Task));
        
        return tasks;
    }

    public record CacheItem
    {
        public int Number { get; set; }
    }

    private static Exception[] UnwrapExceptions<T>(Task<T>[] tasks) =>
        tasks
            .Select(t => t.Exception)
            .Cast<AggregateException>()
            .Select(ae => ae.InnerException!)
            .ToArray();
}
