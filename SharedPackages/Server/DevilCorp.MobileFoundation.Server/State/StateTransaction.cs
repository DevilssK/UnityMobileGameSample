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
/// Read → pure decision → one conditional batch. Every existing key is written back with the writeLock it was
/// read with, so a concurrent change makes the whole batch fail and the decision runs again on fresh data.
/// </summary>
public sealed class StateTransaction
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly IProtectedStateStore _store;
    private readonly IClock _clock;
    private readonly Func<TimeSpan, Task> _delay;
    private readonly ProtectedStateOptions? _guard;

    /// <param name="guard">When set, every run also reads the wallet and refuses to run before it exists, during
    /// its creation grace period, or with an unknown schema. Production code always passes it.</param>
    public StateTransaction(IProtectedStateStore store, IClock clock, Func<TimeSpan, Task>? delay = null, ProtectedStateOptions? guard = null)
    {
        _store = store;
        _clock = clock;
        _delay = delay ?? (d => Task.Delay(d));
        _guard = guard;
    }

    public async Task<TResult> RunAsync<TResult>(
        IExecutionContext context,
        IReadOnlyCollection<string> keys,
        Func<StateSnapshot, StateMutation<TResult>> decide,
        CancellationToken ct)
    {
        var keysToRead = _guard == null ? keys : keys.Append(WalletState.Key).Distinct().ToList();
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var snapshot = new StateSnapshot(await _store.ReadAsync(context, keysToRead, ct));
            if (_guard != null)
            {
                CheckGuard(snapshot, _guard);
            }

            var mutation = decide(snapshot);
            if (mutation.Writes.Count == 0)
            {
                return mutation.Result;
            }

            var writes = mutation.Writes
                .Select(w => new ItemWrite(w.Key, JToken.FromObject(w.Value, StateJson.Serializer), snapshot.WriteLockOf(w.Key)))
                .ToList();
            if (await _store.TryWriteBatchAsync(context, writes, ct))
            {
                return mutation.Result;
            }

            if (attempt < MaxAttempts)
            {
                await _delay(RetryDelay());
            }
        }

        throw new GameOperationException(ErrorCodes.RetryLater, "too many concurrent updates");
    }

    private void CheckGuard(StateSnapshot snapshot, ProtectedStateOptions guard)
    {
        if (!snapshot.TryGet<WalletState>(WalletState.Key, out var wallet))
        {
            throw new GameOperationException(ErrorCodes.RetryLater, "state not initialized");
        }

        if (wallet.SchemaVersion != WalletState.CurrentSchemaVersion)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"unknown wallet schema {wallet.SchemaVersion}");
        }

        if (_clock.UtcNowUnixMs < wallet.CreatedAtUnixMs + (long)guard.GracePeriod.TotalMilliseconds)
        {
            throw new GameOperationException(ErrorCodes.RetryLater, "state was just created");
        }
    }

    private static TimeSpan RetryDelay() =>
        MinRetryDelay + TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * (MaxRetryDelay - MinRetryDelay).TotalMilliseconds);
}
