using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Ads.Tests
{
    internal sealed class FakeAdGateway : IRewardedAdGateway
    {
        public bool IsReady { get; set; } = true;
        public int LoadCalls { get; private set; }
        public List<string> Shows { get; } = new List<string>();

        public event Action? Loaded;
        public event Action<string>? LoadFailed;
        public event Action<AdReward>? Rewarded;
        public event Action? Closed;
        public event Action<string>? DisplayFailed;

        public void Load() => LoadCalls++;

        public void Show(string placement) => Shows.Add(placement);

        public void RaiseLoaded() => Loaded?.Invoke();
        public void RaiseLoadFailed(string reason = "no fill") => LoadFailed?.Invoke(reason);
        public void RaiseRewarded(string name = "coins", int amount = 10) => Rewarded?.Invoke(new AdReward(name, amount));
        public void RaiseClosed() => Closed?.Invoke();
        public void RaiseDisplayFailed(string reason = "display error") => DisplayFailed?.Invoke(reason);
    }

    /// <summary>Delay the test releases by hand, so grace periods and retry delays are deterministic.</summary>
    internal sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource<bool>> _pending = new List<TaskCompletionSource<bool>>();

        public List<TimeSpan> Requested { get; } = new List<TimeSpan>();

        public Task Delay(TimeSpan duration)
        {
            Requested.Add(duration);
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(source);
            return source.Task;
        }

        public int PendingCount => _pending.Count;

        /// <summary>Releases every delay requested so far and lets their continuations run.</summary>
        public async Task ReleaseAllAsync()
        {
            var released = _pending.ToArray();
            _pending.Clear();
            foreach (var source in released)
            {
                source.SetResult(true);
            }

            // Continuations run asynchronously: give them a chance to finish before asserting.
            await Task.Delay(50);
        }
    }
}
