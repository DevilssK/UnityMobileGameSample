using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>The calling player's protected state: readable by the player, writable only by the server.</summary>
public interface IProtectedStateStore
{
    /// <summary>Returns the keys that exist; missing keys are absent from the result.</summary>
    Task<IReadOnlyDictionary<string, StoredItem>> ReadAsync(IExecutionContext context, IReadOnlyCollection<string> keys, CancellationToken ct);

    /// <summary>Writes every key or none. Returns false only on a writeLock conflict; any other failure throws.</summary>
    Task<bool> TryWriteBatchAsync(IExecutionContext context, IReadOnlyList<ItemWrite> writes, CancellationToken ct);
}
