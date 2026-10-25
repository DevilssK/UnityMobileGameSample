using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;

namespace DevilCorp.MobileFoundation.Server.Tests;

/// <summary>
/// In-memory protected state with Cloud Save semantics observed in the development spike: a write with a
/// writeLock must match the stored one, a write without writeLock always succeeds (creates or overwrites),
/// a batch is all-or-nothing. It can also inject conflicts and failures.
/// </summary>
public sealed class FakeProtectedStateStore : IProtectedStateStore
{
    private readonly Dictionary<string, StoredItem> _items = new();
    private int _lockCounter;

    public int ConflictsToInject { get; set; }
    public Exception? FailWith { get; set; }
    public List<IReadOnlyList<ItemWrite>> Batches { get; } = new();
    public int Reads { get; private set; }

    /// <summary>Runs between a read and the following write, to simulate a concurrent writer.</summary>
    public Action? BeforeNextWrite { get; set; }

    public void Seed(string key, object value)
    {
        _items[key] = new StoredItem(key, JToken.FromObject(value), NextLock());
    }

    public StoredItem? Get(string key) => _items.TryGetValue(key, out var item) ? item : null;

    public Task<IReadOnlyDictionary<string, StoredItem>> ReadAsync(IExecutionContext context, IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        Reads++;
        IReadOnlyDictionary<string, StoredItem> result = keys
            .Where(_items.ContainsKey)
            .ToDictionary(k => k, k => _items[k] with { Value = _items[k].Value.DeepClone() });
        return Task.FromResult(result);
    }

    public Task<bool> TryWriteBatchAsync(IExecutionContext context, IReadOnlyList<ItemWrite> writes, CancellationToken ct)
    {
        var hook = BeforeNextWrite;
        BeforeNextWrite = null;
        hook?.Invoke();

        if (FailWith != null)
        {
            throw FailWith;
        }

        if (ConflictsToInject > 0)
        {
            ConflictsToInject--;
            return Task.FromResult(false);
        }

        foreach (var write in writes)
        {
            if (write.WriteLock != null && (!_items.TryGetValue(write.Key, out var stored) || stored.WriteLock != write.WriteLock))
            {
                return Task.FromResult(false);
            }
        }

        Batches.Add(writes);
        foreach (var write in writes)
        {
            _items[write.Key] = new StoredItem(write.Key, write.Value.DeepClone(), NextLock());
        }

        return Task.FromResult(true);
    }

    private string NextLock() => $"lock-{++_lockCounter}";
}

public sealed class ManualClock : IClock
{
    public long UtcNowUnixMs { get; set; } = 1_761_400_000_000;
}
