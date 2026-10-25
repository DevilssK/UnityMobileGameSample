using System.Collections.Generic;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>The items read at the start of one transaction attempt.</summary>
public sealed class StateSnapshot
{
    private readonly IReadOnlyDictionary<string, StoredItem> _items;

    internal StateSnapshot(IReadOnlyDictionary<string, StoredItem> items)
    {
        _items = items;
    }

    public bool Contains(string key) => _items.ContainsKey(key);

    /// <summary>Deserializes a fresh copy: mutating it does not change the snapshot.</summary>
    public bool TryGet<T>(string key, out T value)
    {
        if (_items.TryGetValue(key, out var item))
        {
            value = item.Value.ToObject<T>(StateJson.Serializer)!;
            return true;
        }

        value = default!;
        return false;
    }

    public string? WriteLockOf(string key) => _items.TryGetValue(key, out var item) ? item.WriteLock : null;
}
