namespace DevilCorp.MobileFoundation.Ads
{
    /// <summary>
    /// Terminal outcome of a rewarded ad as seen by the game.
    /// </summary>
    public enum RewardedAdOutcome
    {
        /// <summary>The SDK raised its reward event for this ad: grant the reward once.</summary>
        Rewarded,

        /// <summary>The ad closed without a reward event, or the caller stopped waiting.</summary>
        NotRewarded,

        /// <summary>No ad is loaded; a new load has been requested.</summary>
        Unavailable,

        /// <summary>The ad could not be displayed.</summary>
        Failed,

        /// <summary>Another ad is still on screen.</summary>
        AlreadyShowing
    }
}
