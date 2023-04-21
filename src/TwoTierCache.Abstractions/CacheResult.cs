namespace TwoTierCache.Abstractions;

public readonly struct CacheResult<T>
{
    public CacheResult(T? value)
    {
        Value = value;
        Success = true;
    }
    
    public T? Value { get; }
    
    public bool Success { get; }

    public static CacheResult<T> Miss => new();
    public static CacheResult<T> Found(T? value) => new(value);
}
