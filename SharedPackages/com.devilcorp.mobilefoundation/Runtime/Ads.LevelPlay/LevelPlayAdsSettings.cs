#nullable enable

namespace DevilCorp.MobileFoundation.Ads.LevelPlay
{
    /// <summary>
    /// Public LevelPlay identifiers of one game. They are not secrets, but they differ per game and per
    /// platform: keep them in the game's configuration, not in the package.
    /// </summary>
    public sealed class LevelPlayAdsSettings
    {
        public LevelPlayAdsSettings(
            string androidAppKey,
            string iosAppKey,
            string androidRewardedAdUnitId,
            string iosRewardedAdUnitId,
            bool enableTestSuite = false)
        {
            AndroidAppKey = androidAppKey;
            IosAppKey = iosAppKey;
            AndroidRewardedAdUnitId = androidRewardedAdUnitId;
            IosRewardedAdUnitId = iosRewardedAdUnitId;
            EnableTestSuite = enableTestSuite;
        }

        public string AndroidAppKey { get; }
        public string IosAppKey { get; }
        public string AndroidRewardedAdUnitId { get; }
        public string IosRewardedAdUnitId { get; }

        /// <summary>Opens the LevelPlay test suite after init. Ignored outside development builds.</summary>
        public bool EnableTestSuite { get; }
    }
}
