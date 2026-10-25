#nullable enable

using System;

namespace DevilCorp.MobileFoundation.Ads
{
    /// <summary>
    /// Thin translation of an ad SDK. It holds no reward policy: <see cref="RewardedAdCoordinator"/> does.
    /// Events are raised on the Unity main thread.
    /// </summary>
    public interface IRewardedAdGateway
    {
        bool IsReady { get; }

        event Action? Loaded;

        /// <summary>Readable reason, without personal data.</summary>
        event Action<string>? LoadFailed;

        event Action<AdReward>? Rewarded;
        event Action? Closed;
        event Action<string>? DisplayFailed;

        void Load();
        void Show(string placement);
    }
}
