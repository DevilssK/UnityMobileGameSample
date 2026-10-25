using GemHunterUGSCloud.Game;
using Newtonsoft.Json.Linq;

namespace GemHunterUGSCloud.Tests.Game;

/// <summary>Same values as Assets/UGSConfigs/game_tuning.rc and fetched_keys.rc.</summary>
internal static class TestTuning
{
    public const long NowMs = 1_761_400_000_000;
    public const long NowSec = NowMs / 1000;

    public static readonly JToken WalletDefaults = JToken.Parse(@"{
        ""wallet"": {
            ""currencies"": { ""COIN"": { ""initial"": 200 } },
            ""items"": {
                ""LARGE_BOMB"": { ""initial"": 1 }, ""SMALL_BOMB"": { ""initial"": 3 }, ""HORIZONTAL_ROCKET"": { ""initial"": 1 },
                ""VERTICAL_ROCKET"": { ""initial"": 1 }, ""COLOR_BONUS"": { ""initial"": 1 }
            },
            ""timedEffects"": { ""INFINITE_HEART"": { ""durationSeconds"": 360 } }
        },
        ""player"": { ""hearts"": 8, ""stars"": 5 },
        ""freeCoinPack"": [ { ""id"": ""COIN"", ""amount"": 10 } ]
    }");

    public static readonly JToken AreaItems = JToken.Parse(@"{
        ""items"": {
            ""1"": { ""name"": ""Shark"", ""coinCostPerLevel"": 20, ""maxLevel"": 5 },
            ""2"": { ""name"": ""Seahorse"", ""coinCostPerLevel"": 40, ""maxLevel"": 5 },
            ""3"": { ""name"": ""Submarine"", ""coinCostPerLevel"": 60, ""maxLevel"": 5 },
            ""4"": { ""name"": ""Treasure"", ""coinCostPerLevel"": 80, ""maxLevel"": 5 },
            ""5"": { ""name"": ""Turtle"", ""coinCostPerLevel"": 100, ""maxLevel"": 5 }
        }
    }");

    public static readonly JToken AreaCompleteRewards = JToken.Parse(@"{
        ""area1CompleteRewards"": [
            { ""id"": ""COLOR_BONUS"", ""amount"": 4 }, { ""id"": ""VERTICAL_ROCKET"", ""amount"": 4 }, { ""id"": ""LARGE_BOMB"", ""amount"": 4 }
        ]
    }");

    public static readonly JToken DailyRewards = JToken.Parse(@"{
        ""dailyRewards"": [ { ""id"": ""COIN"", ""quantity"": 10 }, { ""id"": ""COIN"", ""quantity"": 20 }, { ""id"": ""COIN"", ""quantity"": 30 } ],
        ""secondsPerDay"": 86400,
        ""totalDays"": 3,
        ""bonusReward"": []
    }");

    public static GemHunterTuning Create() => GemHunterTuning.From(WalletDefaults, AreaItems, AreaCompleteRewards, DailyRewards);
}
