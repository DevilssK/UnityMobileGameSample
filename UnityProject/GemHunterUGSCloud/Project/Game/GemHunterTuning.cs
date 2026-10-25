using System.Collections.Generic;
using System.Linq;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GemHunterUGSCloud.Game;

/// <summary>Game settings from Remote Config (game_tuning.rc and fetched_keys.rc).</summary>
public sealed class GemHunterTuning
{
    public WalletCatalog Wallet { get; private init; } = new();
    public int InitialHearts { get; private init; }
    public int InitialStars { get; private init; }
    public IReadOnlyList<Grant> FreeCoinPack { get; private init; } = new List<Grant>();
    public IReadOnlyDictionary<int, AreaItemDefinition> AreaItems { get; private init; } = new Dictionary<int, AreaItemDefinition>();
    public IReadOnlyDictionary<string, List<Grant>> AreaCompleteRewards { get; private init; } = new Dictionary<string, List<Grant>>();
    public ConfigData DailyRewards { get; private init; } = new();

    public static GemHunterTuning From(JToken walletDefaults, JToken areaItems, JToken areaCompleteRewards, JToken dailyRewards)
    {
        try
        {
            var defaults = walletDefaults.ToObject<WalletDefaults>() ?? throw Invalid(GemHunterKeys.WalletDefaultsConfig);
            var items = areaItems.ToObject<AreaItemsConfig>() ?? throw Invalid(GemHunterKeys.AreaItemsConfig);
            var tuning = new GemHunterTuning
            {
                Wallet = WalletCatalog.Parse(defaults.Wallet ?? throw Invalid(GemHunterKeys.WalletDefaultsConfig)),
                InitialHearts = defaults.Player?.Hearts ?? throw Invalid(GemHunterKeys.WalletDefaultsConfig),
                InitialStars = defaults.Player.Stars,
                FreeCoinPack = defaults.FreeCoinPack ?? throw Invalid(GemHunterKeys.WalletDefaultsConfig),
                AreaItems = items.Items.ToDictionary(i => int.Parse(i.Key), i => i.Value),
                AreaCompleteRewards = areaCompleteRewards.ToObject<Dictionary<string, List<Grant>>>()
                    ?? throw Invalid(GemHunterKeys.AreaCompleteRewardsConfig),
                DailyRewards = dailyRewards.ToObject<ConfigData>() ?? throw Invalid(GemHunterKeys.DailyRewardsConfig),
            };

            if (tuning.AreaItems.Values.Any(i => i.CoinCostPerLevel <= 0 || i.MaxLevel <= 0)
                || tuning.DailyRewards.DailyRewards == null || tuning.DailyRewards.SecondsPerDay <= 0)
            {
                throw Invalid("game tuning values");
            }

            return tuning;
        }
        catch (JsonException e)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"invalid game tuning: {e.Message}");
        }
        catch (System.FormatException e)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"invalid area item id: {e.Message}");
        }
    }

    private static GameOperationException Invalid(string what) =>
        new(ErrorCodes.ConfigurationError, $"invalid {what}");

    private sealed class WalletDefaults
    {
        [JsonProperty("wallet")] public JToken? Wallet { get; set; }
        [JsonProperty("player")] public PlayerDefaults? Player { get; set; }
        [JsonProperty("freeCoinPack")] public List<Grant>? FreeCoinPack { get; set; }
    }

    private sealed class PlayerDefaults
    {
        [JsonProperty("hearts")] public int Hearts { get; set; }
        [JsonProperty("stars")] public int Stars { get; set; }
    }

    private sealed class AreaItemsConfig
    {
        [JsonProperty("items")] public Dictionary<string, AreaItemDefinition> Items { get; set; } = new();
    }
}

public sealed class AreaItemDefinition
{
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("coinCostPerLevel")] public int CoinCostPerLevel { get; set; }
    [JsonProperty("maxLevel")] public int MaxLevel { get; set; }
}
