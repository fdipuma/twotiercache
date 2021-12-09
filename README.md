# Two-Tier Cache

## Overview

TwoTierCache is an super-simple open source caching implementation for .NET that supports a two-tier strategy for setting and retrieving cached values.

A typical scenario in modern CPUs is to have multiple layer of caching (e.g. L1, L2, L3, RAM, Disk), each with different performance and characteristics.

A two-tier strategy means to give the user the ability to store cached values into a first (fast but volatile) layer and also in a second (slower, persisted and distributed) layer, so different instances of the same application can speed-up the cache retrieving process by looking sequentially on the layers.

This project was inspired by [this post by StackOverflow](https://nickcraver.com/blog/2019/08/06/stack-overflow-how-we-do-app-caching/) on how they manage cache (spoiler: in memory + redis).

## Packages


| Name                                 | Description                                                                                                                  |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------------|
| TwoTierCache.Abstractions            | Interfaces and other abstractions used by the other packages                                                                 |
| TwoTierCache                         | Main implementation of a default `ITwoTierCache` that uses `Microsoft.Extensions.Caching` primitives for `IDistributedCache` |
| TwoTierCache.EvictionSignaling.Redis | Backplane service that uses Redis for syncing in memory evictions from multiple instances (using Redis pub/sub)              |
| TwoTierCache.AspNetCore.TicketStore  | Custom `ITicketStore` for ASP.NET Core to be used as Session Store of `AuthenticationTicket` in case of Cookies Auth         |

## Quick start

### ASP.NET Core App

For a simple scenario that uses `Microsoft.Extensions.Caching` `MemoryCache` as first layer and Redis as second layer inside an AspNetCore app.

1. First install nuget packages

```shell
dotnet add package TwoTierCache
dotnet add package TwoTierCache.EvictionSignaling.Redis
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

2. Add services to service collection (Startup.cs or Program.cs in minimal APIs):
```c#
// first register the two-tiers (in memory + distributed)
services.AddMemoryCache();
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = _config["MyRedisConStr"];
    options.InstanceName = "SampleInstance";
});

// then add two tier cache
services.AddTwoTierCache();

// add eviction signaler so other instances will never serve stale data
services.AddRedisCacheEvictionSignaler(options =>
{
    options.Configuration = _config["MyRedisConStr"];
    options.EvictionChannelName = "MyCustomChannel"; // used to customize which Redis pub/sub channel to use
});
```

3. Use the cache:

```c#
public class SampleController : Controller
{
    private readonly ITodoRepository _repository;
    private readonly ITwoTierCache _cache;

    // inject ITwoTierCache into Controllers or Services
    public SampleController(ITodoRepository repository, ITwoTierCache cache)
    {
        _repository = repository;
        _cache = cache;
    }
    
    [HttpGet("{id}")]
    public async Task<ToDoItem> Get(int id, CancellationToken token)
    {
        var cacheKey = $"todo-{id}";
        var item = await _cache.GetAsync(cacheKey, token);
        
        if (item is null)
        {
            item = await _repository.GetItemAsync(id, token);
            
            if (item is not null)
            {
                await _cache.SetAsync(cacheKey, item, token);
            }
        }
        
        return item;
    }
    
    [HttpPut("{id}")]
    public async Task<ToDoItem> Put(int id, ToDoItem item, CancellationToken token)
    {
        await _repository.UpdateAsync(id, item, token);
        
        // remove item from the cache so we do not serve stale data
        await _cache.RemoveAsync($"todo-{id}", token);
    }    
}
```

### Cookies Authentication SessionStore

In ASP.NET Core Cookies authentication stores all auth session information (e.g. claims) into a cookie.

This makes the entire interaction stateless (which is good) but also allows this cookie to grow indefinitely each time a new claim is added to the user identity.

Another option is to use a custom `SessionStore` where to store `AuthenticationTicket`, so the Cookies Authentication handler sends a cookie with just a reference to the storage instead of serializing the entire user identity.

`TwoTierCache.AspNetCore.TicketStore` allows you to use a Two-Tier cache as session storage for authentication tickets, so you can store tickets both in memory and on distributed cache while keeping in sync instances with an evictio signaler.

1. Install packages
```shell
dotnet add package TwoTierCache
dotnet add package TwoTierCache.EvictionSignaling.Redis
dotnet add package TwoTierCache.AspNetCore.TicketStore
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

2. Configure services
```c#
// register all services as seen in the ASP.NET Core example above
services.AddMemoryCache();
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = _config["MyRedisConStr"];
    options.InstanceName = "SampleInstance";
});

// then add two tier cache
services.AddTwoTierCache();

// add eviction signaler so other instances will never serve stale data
services.AddRedisCacheEvictionSignaler(options =>
{
    options.Configuration = _config["MyRedisConStr"];
    options.EvictionChannelName = "MyCustomChannel"; // used to customize which Redis pub/sub channel to use
});

// cookies auth configuration
services.AddAuthentication("cookies")
    .AddCookie("cookies", o =>
    {
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Cookie.SameSite = SameSiteMode.Lax;        
    });

services.AddTwoTiersCacheTicketStore("cookies"); // use the same authenticationScheme configured in AddCookie
```

## Similar projects
Other awesome (and more complete) projects that do something similar:
- https://github.com/jodydonetti/ZiggyCreatures.FusionCache
- https://github.com/TurnerSoftware/CacheTower
- https://github.com/dotnetcore/EasyCaching
- https://github.com/MichaCo/CacheManager
- https://github.com/FoundatioFx/Foundatio#caching