using System.Collections.Concurrent;

namespace TwoTierCache;

/// <summary>
/// Tracks concurrent async operations so the caller reuses the same task when
/// called in parallel 
/// </summary>
internal class LazyOperationTracker
{
    private readonly ConcurrentDictionary<string, Lazy<object>> _runningOperations = new();

    public void RemoveOperation(string key) => _runningOperations.TryRemove(key, out _);

    public Task<T> GetOrAddOperation<T>(string key, Func<string, Task<T>> valueFactory)
    {
        // we use Lazy<Task<T>> so the ValueFactory method is invoked only once even in case multiple threads get access to
        // the second param of ConcurrentDictionary.GetOrAdd: https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/
        
        var lazyOperation = _runningOperations.GetOrAdd(key, cacheKey =>
        {
            return new Lazy<object>(() => valueFactory.Invoke(cacheKey));
        });

        return (Task<T>)lazyOperation.Value;
    }
}
