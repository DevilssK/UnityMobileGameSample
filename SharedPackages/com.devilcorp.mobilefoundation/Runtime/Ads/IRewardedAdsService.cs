#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Ads
{
    /// <summary>
    /// What the game uses to show rewarded ads. It never calls the ad SDK directly.
    /// A reward is reported once per ad, only when the SDK raised its reward event, never on a plain close.
    /// </summary>
    public interface IRewardedAdsService
    {
        bool IsAdReady { get; }
        bool IsShowing { get; }

        event Action<bool>? AvailabilityChanged;

        /// <summary>
        /// Raised once for every ad that finished, including ads whose caller stopped waiting and ads whose
        /// caller already received the result from <see cref="ShowAsync"/>. Grant a reward either here or from
        /// the <see cref="ShowAsync"/> result, never both.
        /// </summary>
        event Action<RewardedAdResult>? AdCompleted;

        /// <summary>Requests the first load. Calling it again does nothing.</summary>
        void Start();

        /// <summary>
        /// Shows an ad and waits for its outcome. Cancelling only stops waiting: the final result is still
        /// raised through <see cref="AdCompleted"/>.
        /// </summary>
        Task<RewardedAdResult> ShowAsync(string placement, CancellationToken cancellationToken);
    }
}
