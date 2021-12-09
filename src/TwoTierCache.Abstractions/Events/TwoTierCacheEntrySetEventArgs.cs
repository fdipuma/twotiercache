namespace TwoTierCache.Abstractions.Events;

public class TwoTierCacheEntrySetEventArgs : EventArgs
{
    public string Key { get; }
    public object? Entry { get; }

    public TwoTierCacheEntrySetEventArgs(string key, object? entry)
    {
        Key = key;
        Entry = entry;
    }
}
