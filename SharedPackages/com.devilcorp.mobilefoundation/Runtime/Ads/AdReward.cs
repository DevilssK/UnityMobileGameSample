#nullable enable

namespace DevilCorp.MobileFoundation.Ads
{
    /// <summary>
    /// Reward reported by the ad network. Informative only: the game decides what it grants.
    /// </summary>
    public sealed class AdReward
    {
        public AdReward(string name, int amount)
        {
            Name = name;
            Amount = amount;
        }

        public string Name { get; }
        public int Amount { get; }

        public override string ToString() => $"{Name} x{Amount}";
    }
}
