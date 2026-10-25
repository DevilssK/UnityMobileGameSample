#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Ads
{
    /// <summary>
    /// Owns the rewarded ad rules: one ad at a time, a reward only on the SDK reward event, at most one reward
    /// per ad, never a reward on a plain close. Some networks raise the reward event after the close event, so a
    /// close without reward waits a short grace period before concluding <see cref="RewardedAdOutcome.NotRewarded"/>.
    /// </summary>
    public sealed class RewardedAdCoordinator : IRewardedAdsService
    {
        public const int MaxLoadRetries = 3;
        public static readonly TimeSpan DefaultRewardGracePeriod = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan DefaultLoadRetryDelay = TimeSpan.FromSeconds(5);

        private readonly IRewardedAdGateway _gateway;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly TimeSpan _rewardGracePeriod;
        private readonly Action<string> _log;

        private ActiveAd? _current;
        private bool _started;
        private int _loadFailures;

        public RewardedAdCoordinator(
            IRewardedAdGateway gateway,
            Func<TimeSpan, Task>? delay = null,
            TimeSpan? rewardGracePeriod = null,
            Action<string>? log = null)
        {
            _gateway = gateway;
            _delay = delay ?? (duration => Task.Delay(duration));
            _rewardGracePeriod = rewardGracePeriod ?? DefaultRewardGracePeriod;
            _log = log ?? (_ => { });

            _gateway.Loaded += HandleLoaded;
            _gateway.LoadFailed += HandleLoadFailed;
            _gateway.Rewarded += HandleRewarded;
            _gateway.Closed += HandleClosed;
            _gateway.DisplayFailed += HandleDisplayFailed;
        }

        public bool IsAdReady => _current == null && _gateway.IsReady;
        public bool IsShowing => _current != null;

        public event Action<bool>? AvailabilityChanged;
        public event Action<RewardedAdResult>? AdCompleted;

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _gateway.Load();
        }

        public Task<RewardedAdResult> ShowAsync(string placement, CancellationToken cancellationToken)
        {
            if (_current != null)
            {
                return Task.FromResult(new RewardedAdResult(RewardedAdOutcome.AlreadyShowing, placement));
            }

            if (!_gateway.IsReady)
            {
                // An explicit request restarts loading, even after the retry budget was spent.
                _loadFailures = 0;
                _gateway.Load();
                return Task.FromResult(new RewardedAdResult(RewardedAdOutcome.Unavailable, placement));
            }

            var ad = new ActiveAd(placement);
            _current = ad;
            if (cancellationToken.CanBeCanceled)
            {
                ad.Registration = cancellationToken.Register(() =>
                    ad.Waiter.TrySetResult(new RewardedAdResult(RewardedAdOutcome.NotRewarded, placement, message: "wait cancelled")));
            }

            _gateway.Show(placement);
            return ad.Waiter.Task;
        }

        private void HandleRewarded(AdReward reward)
        {
            var ad = _current;
            if (ad == null || ad.Reward != null)
            {
                _log($"Ignored reward {reward}: no ad waiting for one");
                return;
            }

            ad.Reward = reward;
            if (ad.Closed)
            {
                Finish(ad, RewardedAdOutcome.Rewarded);
            }
        }

        private async void HandleClosed()
        {
            var ad = _current;
            if (ad == null || ad.Closed)
            {
                return;
            }

            ad.Closed = true;
            if (ad.Reward != null)
            {
                Finish(ad, RewardedAdOutcome.Rewarded);
                return;
            }

            try
            {
                await _delay(_rewardGracePeriod);
            }
            catch (Exception e)
            {
                _log($"Reward grace period interrupted: {e.Message}");
            }

            if (_current == ad)
            {
                Finish(ad, ad.Reward != null ? RewardedAdOutcome.Rewarded : RewardedAdOutcome.NotRewarded);
            }
        }

        private void HandleDisplayFailed(string reason)
        {
            var ad = _current;
            if (ad != null)
            {
                Finish(ad, RewardedAdOutcome.Failed, reason);
            }
        }

        private void HandleLoaded()
        {
            _loadFailures = 0;
            AvailabilityChanged?.Invoke(true);
        }

        private async void HandleLoadFailed(string reason)
        {
            _loadFailures++;
            if (_loadFailures > MaxLoadRetries)
            {
                _log($"Rewarded ad load failed ({reason}); giving up until the next request");
                AvailabilityChanged?.Invoke(false);
                return;
            }

            _log($"Rewarded ad load failed ({reason}); retry {_loadFailures}/{MaxLoadRetries}");
            try
            {
                await _delay(DefaultLoadRetryDelay);
                _gateway.Load();
            }
            catch (Exception e)
            {
                _log($"Rewarded ad load retry failed: {e.Message}");
            }
        }

        private void Finish(ActiveAd ad, RewardedAdOutcome outcome, string message = "")
        {
            _current = null;
            ad.Registration.Dispose();

            var result = new RewardedAdResult(outcome, ad.Placement, outcome == RewardedAdOutcome.Rewarded ? ad.Reward : null, message);
            ad.Waiter.TrySetResult(result);
            AdCompleted?.Invoke(result);

            // The shown ad is consumed: load the next one.
            AvailabilityChanged?.Invoke(false);
            _loadFailures = 0;
            _gateway.Load();
        }

        private sealed class ActiveAd
        {
            public ActiveAd(string placement)
            {
                Placement = placement;
            }

            public string Placement { get; }
            public TaskCompletionSource<RewardedAdResult> Waiter { get; } = new TaskCompletionSource<RewardedAdResult>();
            public CancellationTokenRegistration Registration { get; set; }
            public AdReward? Reward { get; set; }
            public bool Closed { get; set; }
        }
    }
}
