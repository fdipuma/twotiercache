using System.Text.Json;
using TwoTierCache.Abstractions;

namespace TwoTierCache;

/// <summary>
/// <see cref="DistributedCacheEntry{T}"/> serializer that uses <see cref="System.Text.Json.JsonSerializer"/> under
/// the hood
/// </summary>
public class DistributedCacheEntryJsonSerializer : IDistributedCacheEntrySerializer
{
    private readonly JsonSerializerOptions _jsonOptions;

    public DistributedCacheEntryJsonSerializer()
    {
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };
    }

    public bool CanSerialize(Type type) => true;

    public int Priority => 0;

    public byte[] Serialize<T>(DistributedCacheEntry<T> entry)
    {
        return JsonSerializer.SerializeToUtf8Bytes(entry, _jsonOptions);
    }

    public DistributedCacheEntry<T> Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<DistributedCacheEntry<T>>(bytes, _jsonOptions) ??
               new DistributedCacheEntry<T>();
    }
}
