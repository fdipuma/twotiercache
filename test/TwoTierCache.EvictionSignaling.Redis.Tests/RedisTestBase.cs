using StackExchange.Redis;

namespace TwoTierCache.EvictionSignaling.Redis.Tests;

public class RedisTestBase : IDisposable
{
    public string RedisConnection { get; }
    public ConnectionMultiplexer Multiplexer { get; }

    public RedisTestBase()
    {
        RedisConnection = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "localhost:6379";
        Multiplexer = ConnectionMultiplexer.Connect(RedisConnection);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Multiplexer.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}