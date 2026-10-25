#nullable enable

using System;
using Unity.Services.LevelPlay;
// Inside DevilCorp.MobileFoundation.Ads.LevelPlay, a bare "LevelPlay" resolves to this namespace, not the SDK class.
using LevelPlaySdk = Unity.Services.LevelPlay.LevelPlay;

namespace DevilCorp.MobileFoundation.Ads.LevelPlay
{
    /// <summary>
    /// LevelPlay 9 implementation of <see cref="IRewardedAdGateway"/>. It only translates the SDK: the reward
    /// rules belong to <see cref="RewardedAdCoordinator"/>. A load requested before the SDK finished
    /// initializing runs once it has.
    /// </summary>
    public sealed class LevelPlayRewardedAdGateway : IRewardedAdGateway, IDisposable
    {
        private readonly LevelPlayAdsSettings _settings;
        private readonly Action<string> _log;
        private LevelPlayRewardedAd? _rewardedAd;
        private bool _initializeRequested;
        private bool _initializeFailed;
        private bool _loadRequested;
        private string? _userId;

        public LevelPlayRewardedAdGateway(LevelPlayAdsSettings settings, Action<string>? log = null)
        {
            _settings = settings;
            _log = log ?? (_ => { });
        }

        public event Action? Loaded;
        public event Action<string>? LoadFailed;
        public event Action<AdReward>? Rewarded;
        public event Action? Closed;
        public event Action<string>? DisplayFailed;

        public bool IsReady => _rewardedAd != null && _rewardedAd.IsAdReady();

        private bool TestSuiteEnabled => _settings.EnableTestSuite && UnityEngine.Debug.isDebugBuild;

        /// <summary>
        /// Initializes the SDK once. <paramref name="userId"/> is the player id LevelPlay attaches to
        /// server-to-server callbacks; it is never logged here. After a failed init, the next
        /// <see cref="Load"/> initializes again, so the coordinator's bounded retries cover init too.
        /// </summary>
        public void Initialize(string? userId)
        {
            if (_initializeRequested)
            {
                return;
            }

            _userId = userId;
            StartInit();
        }

        private void StartInit()
        {
            var appKey = PlatformValue(_settings.AndroidAppKey, _settings.IosAppKey);
            if (appKey == null)
            {
                _log("LevelPlay is not supported on this platform");
                return;
            }

            _initializeRequested = true;
            _initializeFailed = false;
            LevelPlaySdk.OnInitSuccess += HandleInitSuccess;
            LevelPlaySdk.OnInitFailed += HandleInitFailed;

            if (TestSuiteEnabled)
            {
                LevelPlaySdk.SetMetaData("is_test_suite", "enable");
            }

            LevelPlaySdk.Init(appKey, _userId);
        }

        public void Load()
        {
            if (PlatformValue(_settings.AndroidRewardedAdUnitId, _settings.IosRewardedAdUnitId) == null)
            {
                LoadFailed?.Invoke("unsupported platform");
                return;
            }

            if (_rewardedAd == null)
            {
                _loadRequested = true;
                if (_initializeFailed)
                {
                    // A failure is reported again through LoadFailed, which keeps the retries bounded.
                    StartInit();
                }

                return;
            }

            _rewardedAd.LoadAd();
        }

        public void Show(string placement)
        {
            if (_rewardedAd == null)
            {
                DisplayFailed?.Invoke("LevelPlay not initialized");
                return;
            }

            _rewardedAd.ShowAd(string.IsNullOrEmpty(placement) ? null : placement);
        }

        public void Dispose()
        {
            LevelPlaySdk.OnInitSuccess -= HandleInitSuccess;
            LevelPlaySdk.OnInitFailed -= HandleInitFailed;

            if (_rewardedAd == null)
            {
                return;
            }

            _rewardedAd.OnAdLoaded -= HandleAdLoaded;
            _rewardedAd.OnAdLoadFailed -= HandleAdLoadFailed;
            _rewardedAd.OnAdDisplayFailed -= HandleAdDisplayFailed;
            _rewardedAd.OnAdRewarded -= HandleAdRewarded;
            _rewardedAd.OnAdClosed -= HandleAdClosed;
            _rewardedAd.DestroyAd();
            _rewardedAd = null;
        }

        private void HandleInitSuccess(LevelPlayConfiguration configuration)
        {
            if (_rewardedAd != null)
            {
                return;
            }

            _log("LevelPlay initialized");
            if (TestSuiteEnabled)
            {
                LevelPlaySdk.LaunchTestSuite();
            }

            var adUnitId = PlatformValue(_settings.AndroidRewardedAdUnitId, _settings.IosRewardedAdUnitId);
            if (adUnitId == null)
            {
                return;
            }

            _rewardedAd = new LevelPlayRewardedAd(adUnitId);
            _rewardedAd.OnAdLoaded += HandleAdLoaded;
            _rewardedAd.OnAdLoadFailed += HandleAdLoadFailed;
            _rewardedAd.OnAdDisplayFailed += HandleAdDisplayFailed;
            _rewardedAd.OnAdRewarded += HandleAdRewarded;
            _rewardedAd.OnAdClosed += HandleAdClosed;

            if (_loadRequested)
            {
                _loadRequested = false;
                _rewardedAd.LoadAd();
            }
        }

        private void HandleInitFailed(LevelPlayInitError error)
        {
            // The next Load (coordinator retry or player request) initializes again.
            _initializeRequested = false;
            _initializeFailed = true;
            LevelPlaySdk.OnInitSuccess -= HandleInitSuccess;
            LevelPlaySdk.OnInitFailed -= HandleInitFailed;
            LoadFailed?.Invoke($"init failed ({error.ErrorCode}): {error.ErrorMessage}");
        }

        private void HandleAdLoaded(LevelPlayAdInfo info) => Loaded?.Invoke();

        private void HandleAdLoadFailed(LevelPlayAdError error) =>
            LoadFailed?.Invoke($"load failed ({error.ErrorCode}): {error.ErrorMessage}");

        private void HandleAdDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error) =>
            DisplayFailed?.Invoke($"display failed ({error.ErrorCode}): {error.ErrorMessage}");

        private void HandleAdRewarded(LevelPlayAdInfo info, LevelPlayReward reward) =>
            Rewarded?.Invoke(new AdReward(reward.Name, reward.Amount));

        private void HandleAdClosed(LevelPlayAdInfo info) => Closed?.Invoke();

        private static string? PlatformValue(string android, string ios)
        {
#if UNITY_ANDROID
            return android;
#elif UNITY_IOS
            return ios;
#else
            return null;
#endif
        }
    }
}
