namespace TwoTierCache.Abstractions.Events;

public class TwoTierCacheEntryRemovedEventArgs : EventArgs
{
    public string Key { get; }

    public TwoTierCacheEntryRemovedEventArgs(string key)
    {
        Key = key;
    }
}