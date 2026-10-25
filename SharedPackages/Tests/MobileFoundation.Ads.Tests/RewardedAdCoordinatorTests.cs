using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevilCorp.MobileFoundation.Ads.Tests
{
    public class RewardedAdCoordinatorTests
    {
        private const string Placement = "Main_Menu";

        private readonly FakeAdGateway _gateway = new FakeAdGateway();
        private readonly ManualDelay _delay = new ManualDelay();
        private readonly List<RewardedAdResult> _completed = new List<RewardedAdResult>();
        private readonly List<bool> _availability = new List<bool>();

        private RewardedAdCoordinator CreateCoordinator()
        {
            var coordinator = new RewardedAdCoordinator(_gateway, _delay.Delay);
            coordinator.AdCompleted += _completed.Add;
            coordinator.AvailabilityChanged += _availability.Add;
            return coordinator;
        }

        [Fact]
        public async Task ShowAsync_WhenRewardedThenClosed_ReturnsRewardedOnce()
        {
            var coordinator = CreateCoordinator();
            var show = coordinator.ShowAsync(Placement, CancellationToken.None);

            _gateway.RaiseRewarded("coins", 10);
            _gateway.RaiseClosed();
            var result = await show;

            Assert.Equal(RewardedAdOutcome.Rewarded, result.Outcome);
            Assert.Equal("coins", result.Reward!.Name);
            Assert.Equal(10, result.Reward.Amount);
            Assert.Single(_completed);
            Assert.Equal(new[] { Placement }, _gateway.Shows);
        }

        [Fact]
        public async Task ShowAsync_WhenClosedWithoutReward_ReturnsNotRewardedAfterGrace()
        {
            var coordinator = CreateCoordinator();
            var show = coordinator.ShowAsync(Placement, CancellationToken.None);

            _gateway.RaiseClosed();
            Assert.False(show.IsCompleted);

            await _delay.ReleaseAllAsync();
            var result = await show;

            Assert.Equal(RewardedAdOutcome.NotRewarded, result.Outcome);
            Assert.Null(result.Reward);
            Assert.Equal(new[] { RewardedAdCoordinator.DefaultRewardGracePeriod }, _delay.Requested);
        }

        [Fact]
        public async Task Reward_ArrivingAfterClose_WithinGrace_IsGranted()
        {
            var coordinator = CreateCoordinator();
            var show = coordinator.ShowAsync(Placement, CancellationToken.None);

            _gateway.RaiseClosed();
            _gateway.RaiseRewarded("coins", 10);
            await _delay.ReleaseAllAsync();
            var result = await show;

            Assert.Equal(RewardedAdOutcome.Rewarded, result.Outcome);
            Assert.Single(_completed);
        }

        [Fact]
        public async Task Reward_RaisedTwice_IsGrantedOnce()
        {
            var coordinator = CreateCoordinator();
            var show = coordinator.ShowAsync(Placement, CancellationToken.None);

            _gateway.RaiseRewarded("coins", 10);
            _gateway.RaiseRewarded("coins", 99);
            _gateway.RaiseClosed();
            _gateway.RaiseRewarded("coins", 99);
            var result = await show;

            Assert.Equal(10, result.Reward!.Amount);
            Assert.Single(_completed);
        }

        [Fact]
        public void Reward_OutsideShow_IsIgnored()
        {
            CreateCoordinator();

            _gateway.RaiseRewarded();
            _gateway.RaiseClosed();

            Assert.Empty(_completed);
        }

        [Fact]
        public async Task ShowAsync_WhenNotReady_ReturnsUnavailableAndRequestsLoad()
        {
            _gateway.IsReady = false;
            var coordinator = CreateCoordinator();

            var result = await coordinator.ShowAsync(Placement, CancellationToken.None);

            Assert.Equal(RewardedAdOutcome.Unavailable, result.Outcome);
            Assert.Empty(_gateway.Shows);
            Assert.Equal(1, _gateway.LoadCalls);
        }

        [Fact]
        public async Task ShowAsync_WhileShowing_ReturnsAlreadyShowing()
        {
            var coordinator = CreateCoordinator();
            var first = coordinator.ShowAsync(Placement, CancellationToken.None);

            var second = await coordinator.ShowAsync(Placement, CancellationToken.None);

            Assert.Equal(RewardedAdOutcome.AlreadyShowing, second.Outcome);
            Assert.False(first.IsCompleted);
            Assert.True(coordinator.IsShowing);
            Assert.Single(_gateway.Shows);
        }

        [Fact]
        public async Task ShowAsync_WhenDisplayFails_ReturnsFailedAndReloads()
        {
            var coordinator = CreateCoordinator();
            var show = coordinator.ShowAsync(Placement, CancellationToken.None);

            _gateway.RaiseDisplayFailed();
            var result = await show;

            Assert.Equal(RewardedAdOutcome.Failed, result.Outcome);
            Assert.False(coordinator.IsShowing);
            Assert.Equal(1, _gateway.LoadCalls);
            Assert.False(_availability[_availability.Count - 1]);
        }

        [Fact]
        public async Task LoadFailures_StopAfterMaxRetries()
        {
            var coordinator = CreateCoordinator();
            coordinator.Start();

            for (var attempt = 0; attempt <= RewardedAdCoordinator.MaxLoadRetries; attempt++)
            {
                _gateway.RaiseLoadFailed();
                await _delay.ReleaseAllAsync();
            }

            Assert.Equal(1 + RewardedAdCoordinator.MaxLoadRetries, _gateway.LoadCalls);
            Assert.Equal(0, _delay.PendingCount);
            Assert.False(_availability[_availability.Count - 1]);
        }

        [Fact]
        public async Task Loaded_AfterFailures_ResetsRetries()
        {
            var coordinator = CreateCoordinator();
            coordinator.Start();
            _gateway.RaiseLoadFailed();
            await _delay.ReleaseAllAsync();

            _gateway.RaiseLoaded();

            Assert.True(_availability[_availability.Count - 1]);
            for (var attempt = 0; attempt < RewardedAdCoordinator.MaxLoadRetries; attempt++)
            {
                _gateway.RaiseLoadFailed();
                await _delay.ReleaseAllAsync();
            }
            Assert.Equal(2 + RewardedAdCoordinator.MaxLoadRetries, _gateway.LoadCalls);
        }

        [Fact]
        public async Task ShowAsync_WhenCallerCancels_ResultStillRaisedThroughAdCompleted()
        {
            var coordinator = CreateCoordinator();
            using var cancellation = new CancellationTokenSource();
            var show = coordinator.ShowAsync(Placement, cancellation.Token);

            cancellation.Cancel();
            var waited = await show;
            _gateway.RaiseRewarded("coins", 10);
            _gateway.RaiseClosed();

            Assert.Equal(RewardedAdOutcome.NotRewarded, waited.Outcome);
            Assert.True(coordinator.IsShowing == false);
            Assert.Single(_completed);
            Assert.Equal(RewardedAdOutcome.Rewarded, _completed[0].Outcome);
        }

        [Fact]
        public void Start_IsIdempotent()
        {
            var coordinator = CreateCoordinator();

            coordinator.Start();
            coordinator.Start();

            Assert.Equal(1, _gateway.LoadCalls);
        }

        [Fact]
        public void RewardedAdResult_ToString_ContainsOutcomeAndPlacement()
        {
            var text = new RewardedAdResult(RewardedAdOutcome.Rewarded, Placement, new AdReward("coins", 10)).ToString();

            Assert.Contains("Rewarded", text);
            Assert.Contains(Placement, text);
        }
    }
}
