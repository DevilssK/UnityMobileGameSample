using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using NSubstitute;
using Unity.Services.CloudCode.Core;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class StateTransactionTests
{
    private sealed class Counter
    {
        public int Value { get; set; }
    }

    private readonly FakeProtectedStateStore _store = new();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();
    private readonly List<TimeSpan> _delays = new();

    private StateTransaction Transaction() => new(_store, new ManualClock(), d =>
    {
        _delays.Add(d);
        return Task.CompletedTask;
    });

    private Task<int> Increment(StateTransaction transaction) =>
        transaction.RunAsync(_context, new[] { "COUNTER" }, snapshot =>
        {
            var counter = snapshot.TryGet<Counter>("COUNTER", out var c) ? c : new Counter();
            counter.Value++;
            return StateMutation.Write(counter.Value, ("COUNTER", counter));
        }, CancellationToken.None);

    [Fact]
    public async Task Run_NoConflict_WritesOnceWithReadWriteLocks()
    {
        _store.Seed("COUNTER", new Counter { Value = 4 });
        var lockBefore = _store.Get("COUNTER")!.WriteLock;

        var result = await Increment(Transaction());

        Assert.Equal(5, result);
        var write = Assert.Single(Assert.Single(_store.Batches));
        Assert.Equal(lockBefore, write.WriteLock);
        Assert.Equal(5, _store.Get("COUNTER")!.Value["Value"]!.ToObject<int>());
    }

    [Fact]
    public async Task Run_KeyAbsent_WritesWithNullWriteLock()
    {
        await Increment(Transaction());

        var write = Assert.Single(Assert.Single(_store.Batches));
        Assert.Null(write.WriteLock);
    }

    [Fact]
    public async Task Run_ConflictThenSuccess_RereadsAndRetries()
    {
        _store.Seed("COUNTER", new Counter { Value = 1 });
        // A concurrent writer bumps the counter between our read and our write.
        _store.BeforeNextWrite = () => _store.Seed("COUNTER", new Counter { Value = 10 });

        var result = await Increment(Transaction());

        Assert.Equal(11, result);
        Assert.Equal(2, _store.Reads);
        Assert.Single(_delays);
        Assert.InRange(_delays[0], TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task Run_FiveConflicts_ThrowsRetryLaterWithoutPartialWrite()
    {
        _store.Seed("COUNTER", new Counter { Value = 1 });
        _store.ConflictsToInject = StateTransaction.MaxAttempts;

        var error = await Assert.ThrowsAsync<GameOperationException>(() => Increment(Transaction()));

        Assert.Equal(ErrorCodes.RetryLater, error.Code);
        Assert.StartsWith("RETRY_LATER:", error.Message);
        Assert.Empty(_store.Batches);
        Assert.Equal(StateTransaction.MaxAttempts, _store.Reads);
    }

    [Fact]
    public async Task Run_DecideThrowsGameOperation_NoWrite()
    {
        var transaction = Transaction();

        var error = await Assert.ThrowsAsync<GameOperationException>(() =>
            transaction.RunAsync<int>(_context, new[] { "COUNTER" },
                _ => throw new GameOperationException(ErrorCodes.InsufficientFunds, "need 20 COIN"),
                CancellationToken.None));

        Assert.Equal("INSUFFICIENT_FUNDS: need 20 COIN", error.Message);
        Assert.Empty(_store.Batches);
    }

    [Fact]
    public async Task Run_NoWrites_ReturnsResultWithoutBatch()
    {
        var result = await Transaction().RunAsync(_context, new[] { "COUNTER" },
            _ => StateMutation.Read(42), CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Empty(_store.Batches);
    }
}
