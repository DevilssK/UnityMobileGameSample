using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using NSubstitute;
using Unity.Services.CloudCode.Core;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class ProtectedStateInitializerTests
{
    private readonly FakeProtectedStateStore _store = new();
    private readonly ManualClock _clock = new();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();

    private ProtectedStateInitializer Initializer() => new(_store, _clock);

    private StateTransaction GuardedTransaction() =>
        new(_store, _clock, _ => Task.CompletedTask, new ProtectedStateOptions());

    private static IReadOnlyDictionary<string, object> InitialState(long nowMs) => new Dictionary<string, object>
    {
        [WalletState.Key] = new WalletState { CreatedAtUnixMs = nowMs, Currencies = { ["COIN"] = 200 } },
        ["GAME_DATA"] = new { level = 1 },
    };

    private Task<long> AddCoins(long amount) =>
        GuardedTransaction().RunAsync(_context, Array.Empty<string>(), snapshot =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            wallet.Currencies["COIN"] += amount;
            return StateMutation.Write(wallet.Currencies["COIN"], (WalletState.Key, wallet));
        }, CancellationToken.None);

    [Fact]
    public async Task Ensure_WalletAbsent_CreatesAllKeysInOneBatch()
    {
        var created = await Initializer().EnsureInitializedAsync(_context, InitialState, CancellationToken.None);

        Assert.True(created);
        var batch = Assert.Single(_store.Batches);
        Assert.Equal(2, batch.Count);
        Assert.All(batch, w => Assert.Null(w.WriteLock));
        Assert.Equal(_clock.UtcNowUnixMs, _store.Get(WalletState.Key)!.Value["createdAtUnixMs"]!.ToObject<long>());
    }

    [Fact]
    public async Task Ensure_WalletPresent_DoesNotWrite()
    {
        _store.Seed(WalletState.Key, new WalletState { CreatedAtUnixMs = 1 });

        var created = await Initializer().EnsureInitializedAsync(_context, InitialState, CancellationToken.None);

        Assert.False(created);
        Assert.Empty(_store.Batches);
    }

    [Fact]
    public async Task Ensure_InitialStateWithoutWallet_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Initializer().EnsureInitializedAsync(_context,
            _ => new Dictionary<string, object> { ["GAME_DATA"] = new { level = 1 } }, CancellationToken.None));
    }

    [Fact]
    public async Task Run_WalletAbsent_ThrowsRetryLater()
    {
        var error = await Assert.ThrowsAsync<GameOperationException>(() => AddCoins(10));

        Assert.Equal(ErrorCodes.RetryLater, error.Code);
        Assert.Empty(_store.Batches);
    }

    [Fact]
    public async Task Run_DuringGracePeriod_ThrowsRetryLater()
    {
        await Initializer().EnsureInitializedAsync(_context, InitialState, CancellationToken.None);
        _clock.UtcNowUnixMs += 29_999;

        var error = await Assert.ThrowsAsync<GameOperationException>(() => AddCoins(10));

        Assert.Equal(ErrorCodes.RetryLater, error.Code);
        Assert.Single(_store.Batches);
    }

    [Fact]
    public async Task Run_AtExactEndOfGracePeriod_IsAccepted()
    {
        await Initializer().EnsureInitializedAsync(_context, InitialState, CancellationToken.None);
        _clock.UtcNowUnixMs += (long)ProtectedStateOptions.DefaultGracePeriod.TotalMilliseconds;

        var balance = await AddCoins(10);

        Assert.Equal(210, balance);
    }

    [Fact]
    public async Task Run_UnknownSchemaVersion_ThrowsConfigurationError()
    {
        _store.Seed(WalletState.Key, new WalletState { SchemaVersion = 99, CreatedAtUnixMs = 1 });

        var error = await Assert.ThrowsAsync<GameOperationException>(() => AddCoins(10));

        Assert.Equal(ErrorCodes.ConfigurationError, error.Code);
        Assert.Empty(_store.Batches);
    }

    [Fact]
    public async Task ConcurrentInitWithMutationDuringGrace_MutationRejectedAndNothingLost()
    {
        GameOperationException? mutationError = null;
        // Initializer B reads "absent"; before its write lands, initializer A completes and a mutation is attempted.
        _store.BeforeNextWrite = () =>
        {
            Initializer().EnsureInitializedAsync(_context, InitialState, CancellationToken.None).GetAwaiter().GetResult();
            _clock.UtcNowUnixMs += 1_000;
            try
            {
                AddCoins(1_000).GetAwaiter().GetResult();
            }
            catch (GameOperationException e)
            {
                mutationError = e;
            }
        };

        await Initializer().EnsureInitializedAsync(_context, InitialState, CancellationToken.None);

        Assert.Equal(ErrorCodes.RetryLater, mutationError?.Code);
        Assert.Equal(2, _store.Batches.Count); // both initial writes, no mutation
        Assert.Equal(200, _store.Get(WalletState.Key)!.Value["currencies"]!["COIN"]!.ToObject<long>());
    }
}
