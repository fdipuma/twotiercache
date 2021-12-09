using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TwoTierCache.Abstractions;
using TwoTierCache.Abstractions.Events;

namespace TwoTierCache.EvictionSignaling.Redis;

public class RedisCacheEvictionSignaler : BackgroundService
{
    private readonly ITwoTierCache _cache;
    private readonly RedisCacheEvictionSignalerOptions _options;
    private readonly RedisChannel _redisChannel;

    private readonly ConcurrentDictionary<string, byte> _localEvictionKeys = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Channel<string> _keysToEvictRemotelyChannel = Channel.CreateUnbounded<string>();
    
    private ISubscriber? _subscriber;
    private IConnectionMultiplexer? _connection;

    public RedisCacheEvictionSignaler(IOptions<RedisCacheEvictionSignalerOptions> options, ITwoTierCache cache)
    {
        _cache = cache;
        _options = options.Value;
        _redisChannel = new RedisChannel(_options.EvictionChannelName, RedisChannel.PatternMode.Literal);
        
        _cache.EntrySet += OnEntrySet;
        _cache.EntryRemoved += OnEntryRemoved;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var keyToEvictRemotely in _keysToEvictRemotelyChannel.Reader.ReadAllAsync(stoppingToken))
        {
            await SignalCacheEvictionAsync(keyToEvictRemotely, stoppingToken);
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = await ConnectAsync(cancellationToken);

        var subscription = await subscriber.SubscribeAsync(_redisChannel).ConfigureAwait(false);

        subscription.OnMessage(message =>
        {
            string keyToEvict = message.Message;

            var shouldSkipLocalEviction = _localEvictionKeys.TryRemove(keyToEvict, out _);

            if (shouldSkipLocalEviction)
            {
                // we are the service who started the signaling of this key in the first place
                // so no need to evict locally

                return;
            }

            _cache.EvictLocal(keyToEvict);
        });

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.UnsubscribeAllAsync().ConfigureAwait(false);
        }

        await base.StopAsync(cancellationToken);
    }
    
    private void NotifyEviction(string locallyEvictedKey)
    {
        _keysToEvictRemotelyChannel.Writer.TryWrite(locallyEvictedKey);
    }

    private async Task<ISubscriber> ConnectAsync(CancellationToken token = default(CancellationToken))
    {
        token.ThrowIfCancellationRequested();

        if (_subscriber is not null)
        {
            return _subscriber;
        }

        await _connectionLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_subscriber is null)
            {
                if (_options.ConnectionMultiplexerFactory is null)
                {
                    if (_options.ConfigurationOptions is not null)
                    {
                        _connection = await ConnectionMultiplexer.ConnectAsync(_options.ConfigurationOptions)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        _connection = await ConnectionMultiplexer.ConnectAsync(_options.Configuration)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    _connection = await _options.ConnectionMultiplexerFactory();
                }

                _subscriber = _connection.GetSubscriber();
            }
        }
        finally
        {
            _connectionLock.Release();
        }

        return _subscriber;
    }
    
    private void OnEntryRemoved(object? sender, TwoTierCacheEntryRemovedEventArgs e) => NotifyEviction(e.Key);

    private void OnEntrySet(object? sender, TwoTierCacheEntrySetEventArgs e) => NotifyEviction(e.Key);
    
    private async Task SignalCacheEvictionAsync(string key, CancellationToken cancellationToken = default)
    {
        var subscriber = await ConnectAsync(cancellationToken);

        _localEvictionKeys[key] = 0;
        await subscriber.PublishAsync(_redisChannel, key, CommandFlags.FireAndForget);
    }

    public override void Dispose()
    {
        _connectionLock.Dispose();
        _connection?.Dispose();
        
        _cache.EntrySet -= OnEntrySet;
        _cache.EntryRemoved -= OnEntryRemoved;
        
        _keysToEvictRemotelyChannel.Writer.Complete();
        
        base.Dispose();
    }
}
