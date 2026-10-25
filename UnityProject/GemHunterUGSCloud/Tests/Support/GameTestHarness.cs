using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Tests;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Tests.Game;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Tests.Support;

/// <summary>Real game state wiring over an in-memory Cloud Save, with fixed tuning.</summary>
internal sealed class GameTestHarness
{
    public FakeProtectedStateStore Store { get; } = new();
    public ManualClock Clock { get; } = new() { UtcNowUnixMs = TestTuning.NowMs };
    public GemHunterState State { get; }

    public GameTestHarness()
    {
        State = new GemHunterState(Store, Clock, new FixedTuningSource(TestTuning.Create()), _ => Task.CompletedTask);
    }

    public static IExecutionContext Context(string environment = "development")
    {
        var context = Substitute.For<IExecutionContext>();
        context.EnvironmentName.Returns(environment);
        context.PlayerId.Returns("player-1");
        context.CorrelationId.Returns("correlation-1");
        return context;
    }

    /// <summary>Creates the player and moves the clock past the creation grace period.</summary>
    public async Task<IExecutionContext> NewPlayerAsync(string environment = "development")
    {
        var context = Context(environment);
        await State.EnsureInitializedAsync(context, CancellationToken.None);
        Clock.UtcNowUnixMs += 31_000;
        return context;
    }

    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;

    private sealed class FixedTuningSource : ITuningSource
    {
        private readonly GemHunterTuning _tuning;

        public FixedTuningSource(GemHunterTuning tuning)
        {
            _tuning = tuning;
        }

        public Task<GemHunterTuning> GetAsync(IExecutionContext context, CancellationToken ct) => Task.FromResult(_tuning);
    }
}
