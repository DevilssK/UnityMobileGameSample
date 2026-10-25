using System;
using System.Threading;
using DevilCorp.MobileFoundation.Ads;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Ads
{
    /// <summary>
    /// Bridges the hub ad button to the foundation rewarded ads service. It never talks to the ad SDK.
    /// The reward is a local, low-value effect (decision 0001): this sample only reports it, nothing is credited.
    /// </summary>
    public class AdRewardManagerClient : MonoBehaviour
    {
        // Cooldown between rewarded ad completions: UX pacing only, not an anti-cheat limit.
        private const float k_RewardCooldownSeconds = 3;

        private IRewardedAdsService m_RewardedAds;
        private DateTime m_LastAdCompletionTime;

        // Cancels only the UI wait when the hub closes; the ad result still arrives through AdCompleted.
        private readonly CancellationTokenSource m_WaitCancellation = new();

        /// <summary>True when the ad granted its reward, false when it closed early or failed.</summary>
        public event Action<bool> AdSuccessfullyCompleted;
        public event Action<bool> AdAvailable;

        private void Start()
        {
            m_RewardedAds = GameSystemLocator.Get<IRewardedAdsService>();
            m_RewardedAds.AvailabilityChanged += HandleAvailabilityChanged;
        }

        private void OnDestroy()
        {
            m_WaitCancellation.Cancel();
            m_WaitCancellation.Dispose();

            if (m_RewardedAds != null)
            {
                m_RewardedAds.AvailabilityChanged -= HandleAvailabilityChanged;
            }
        }

        /// <summary>
        /// User-facing method to show a rewarded ad when a button is clicked.
        /// </summary>
        /// <param name="placementName">Optional placement name for analytics and capping. Can be null.</param>
        public async void ClickShowAdReward(string placementName = null)
        {
            // Not ready is left to ShowAsync: it answers Unavailable and requests a new load.
            if (m_RewardedAds == null || GetRemainingCooldownSeconds() > 0f)
            {
                Logger.LogWarning($"Cannot show ad for placement: {placementName ?? "default"}");
                AdSuccessfullyCompleted?.Invoke(false);
                return;
            }

            try
            {
                var result = await m_RewardedAds.ShowAsync(placementName ?? string.Empty, m_WaitCancellation.Token);
                m_LastAdCompletionTime = DateTime.UtcNow;

                if (result.Outcome == RewardedAdOutcome.Rewarded)
                {
                    // A real game grants its local effect here (extra moves, booster...).
                    Logger.LogDemo($"🎁 Ad reward earned: {result.Reward}");
                    AdSuccessfullyCompleted?.Invoke(true);
                    return;
                }

                Logger.LogWarning($"Ad not rewarded: {result}");
                AdSuccessfullyCompleted?.Invoke(false);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                AdSuccessfullyCompleted?.Invoke(false);
            }
        }

        /// <summary>
        /// Checks if a rewarded ad can currently be shown.
        /// </summary>
        public bool CanShowAd(string placementName = null)
        {
            return m_RewardedAds != null && m_RewardedAds.IsAdReady && GetRemainingCooldownSeconds() <= 0f;
        }

        /// <summary>
        /// Gets remaining cooldown time in seconds, or 0 if cooldown expired
        /// </summary>
        public float GetRemainingCooldownSeconds()
        {
            if (m_LastAdCompletionTime == default)
                return 0f;

            TimeSpan timeSinceLastAd = DateTime.UtcNow - m_LastAdCompletionTime;
            float remaining = k_RewardCooldownSeconds - (float)timeSinceLastAd.TotalSeconds;
            return Math.Max(0f, remaining);
        }

        private void HandleAvailabilityChanged(bool isAvailable)
        {
            AdAvailable?.Invoke(isAvailable);
        }
    }
}
