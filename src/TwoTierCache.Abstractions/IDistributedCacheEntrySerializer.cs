namespace TwoTierCache.Abstractions;

/// <summary>
/// Represents a serializer and deserializer of <see cref="DistributedCacheEntry{T}"/> used to stream data
/// inside the distributed cache component of a <see cref="ITwoTierCache"/>
/// </summary>
/// <typeparam name="T">The type to serialize</typeparam>
public interface IDistributedCacheEntrySerializer
{
    public bool CanSerialize(Type type);
    int Priority { get; }
    byte[] Serialize<T>(DistributedCacheEntry<T> entry);
    DistributedCacheEntry<T> Deserialize<T>(byte[] bytes);
}
