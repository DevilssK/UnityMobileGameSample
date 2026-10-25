using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Wallet;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>
/// Creates a player's protected state. This is the only unconditional write: it happens only when this same
/// execution has just read the wallet as absent; <see cref="ProtectedStateOptions"/> explains why that is safe.
/// </summary>
public sealed class ProtectedStateInitializer
{
    private readonly IProtectedStateStore _store;
    private readonly IClock _clock;

    public ProtectedStateInitializer(IProtectedStateStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <param name="initialState">Builds every initial key from the read time; must include the wallet with
    /// <see cref="WalletState.CreatedAtUnixMs"/> set to that time.</param>
    /// <returns>True if this call created the state.</returns>
    public async Task<bool> EnsureInitializedAsync(
        IExecutionContext context,
        Func<long, IReadOnlyDictionary<string, object>> initialState,
        CancellationToken ct)
    {
        var existing = await _store.ReadAsync(context, new[] { WalletState.Key }, ct);
        if (existing.ContainsKey(WalletState.Key))
        {
            return false;
        }

        var readAtMs = _clock.UtcNowUnixMs;
        var state = initialState(readAtMs);
        if (!state.TryGetValue(WalletState.Key, out var wallet) || wallet is not WalletState walletState
            || walletState.CreatedAtUnixMs != readAtMs)
        {
            throw new InvalidOperationException("The initial state must contain a WalletState created at the read time.");
        }

        var writes = state
            .Select(entry => new ItemWrite(entry.Key, JToken.FromObject(entry.Value, StateJson.Serializer), null))
            .ToList();
        // A conflict cannot happen without writeLocks; any other failure throws and nothing was created.
        await _store.TryWriteBatchAsync(context, writes, ct);
        return true;
    }
}
