using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Config;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Game;

public sealed record GameSnapshot(WalletState Wallet, PlayerData Player, DailyRewardsPlayerStatus Daily, GemHunterTuning Tuning);

/// <summary>
/// Gem Hunter's protected state: created on first use, then changed only through guarded transactions
/// (one atomic batch per operation, refused during the creation grace period).
/// </summary>
public sealed class GemHunterState
{
    private static readonly string[] AllKeys = { WalletState.Key, GemHunterKeys.PlayerData, GemHunterKeys.DailyRewardsStatus };

    private readonly IProtectedStateStore _store;
    private readonly ITuningSource _tuning;
    private readonly ProtectedStateInitializer _initializer;

    public GemHunterState(IGameApiClient api)
        : this(new CloudSaveProtectedStateStore(api), new SystemClock(), api)
    {
    }

    private GemHunterState(IProtectedStateStore store, IClock clock, IGameApiClient api)
        : this(store, clock, new RemoteConfigTuningSource(new RemoteConfigReader(api, clock)))
    {
    }

    internal GemHunterState(IProtectedStateStore store, IClock clock, ITuningSource tuning, Func<TimeSpan, Task>? delay = null)
    {
        _store = store;
        _tuning = tuning;
        Clock = clock;
        _initializer = new ProtectedStateInitializer(store, clock);
        Transaction = new StateTransaction(store, clock, delay, new ProtectedStateOptions());
    }

    public IClock Clock { get; }

    /// <summary>Always guarded: never constructed without <see cref="ProtectedStateOptions"/>.</summary>
    public StateTransaction Transaction { get; }

    public long NowUnixSec => Clock.UtcNowUnixMs / 1000;

    public Task<GemHunterTuning> TuningAsync(IExecutionContext context, CancellationToken ct) => _tuning.GetAsync(context, ct);

    /// <returns>True if this call created the player's state.</returns>
    public async Task<bool> EnsureInitializedAsync(IExecutionContext context, CancellationToken ct)
    {
        var tuning = await TuningAsync(context, ct);
        return await _initializer.EnsureInitializedAsync(context, nowMs => GemHunterInitialState.Build(tuning, nowMs), ct);
    }

    /// <summary>Reads every key; allowed during the grace period since nothing is written.</summary>
    public async Task<GameSnapshot> ReadAsync(IExecutionContext context, CancellationToken ct)
    {
        await EnsureInitializedAsync(context, ct);
        var tuning = await TuningAsync(context, ct);
        var items = await _store.ReadAsync(context, AllKeys, ct);
        return new GameSnapshot(
            Get<WalletState>(items, WalletState.Key) ?? throw Missing(WalletState.Key),
            Get<PlayerData>(items, GemHunterKeys.PlayerData) ?? throw Missing(GemHunterKeys.PlayerData),
            Get<DailyRewardsPlayerStatus>(items, GemHunterKeys.DailyRewardsStatus) ?? new DailyRewardsPlayerStatus { StartEpochTime = Clock.UtcNowUnixMs },
            tuning);
    }

    /// <summary>Runs one guarded operation over <paramref name="keys"/> (the wallet is always read too).</summary>
    public async Task<T> RunAsync<T>(
        IExecutionContext context,
        IReadOnlyCollection<string> keys,
        Func<StateSnapshot, GemHunterTuning, StateMutation<T>> decide,
        CancellationToken ct)
    {
        await EnsureInitializedAsync(context, ct);
        var tuning = await TuningAsync(context, ct);
        return await Transaction.RunAsync(context, keys, snapshot => decide(snapshot, tuning), ct);
    }

    private static T? Get<T>(IReadOnlyDictionary<string, StoredItem> items, string key) where T : class =>
        items.TryGetValue(key, out var item) ? item.Value.ToObject<T>() : null;

    private static GameOperationException Missing(string key) =>
        new(ErrorCodes.RetryLater, $"{key} not readable yet");
}
