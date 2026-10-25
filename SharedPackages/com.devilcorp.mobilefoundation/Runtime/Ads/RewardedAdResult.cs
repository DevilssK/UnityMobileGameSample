#nullable enable

namespace DevilCorp.MobileFoundation.Ads
{
    public sealed class RewardedAdResult
    {
        public RewardedAdResult(RewardedAdOutcome outcome, string placement, AdReward? reward = null, string message = "")
        {
            Outcome = outcome;
            Placement = placement;
            Reward = reward;
            Message = message;
        }

        public RewardedAdOutcome Outcome { get; }
        public string Placement { get; }

        /// <summary>Set only when <see cref="Outcome"/> is <see cref="RewardedAdOutcome.Rewarded"/>.</summary>
        public AdReward? Reward { get; }

        /// <summary>Short diagnostic for logs.</summary>
        public string Message { get; }

        public override string ToString() => $"{Placement}: {Outcome} {Reward} {Message}".TrimEnd();
    }
}
