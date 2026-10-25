using System.Collections.Generic;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;

namespace GemHunterUGSCloud.Game;

/// <summary>Every protected key of a new player, written once by the guarded initialization.</summary>
public static class GemHunterInitialState
{
    private const int FirstAreaId = 1;
    private const int FirstItemId = 1;

    public static IReadOnlyDictionary<string, object> Build(GemHunterTuning tuning, long nowMs)
    {
        var firstArea = new AreaData
        {
            AreaName = "Area1",
            AreaLevel = FirstAreaId,
            CurrentProgress = 0,
            MaxProgress = 25,
            TotalUpgradableSlots = 5,
            UnlockRequirement_Stars = 1,
            UpgradableAreaItems = new List<UpgradableAreaItem>(),
        };
        firstArea.UpgradableAreaItems.Add(AreaRules.NewItem(FirstItemId, tuning.AreaItems[FirstItemId], unlocked: true));

        return new Dictionary<string, object>
        {
            [WalletState.Key] = WalletRules.Initial(tuning.Wallet, nowMs),
            [GemHunterKeys.PlayerData] = new PlayerData
            {
                Hearts = tuning.InitialHearts,
                Stars = tuning.InitialStars,
                CurrentArea = firstArea,
                GameAreasData = new List<AreaData> { firstArea },
            },
            [GemHunterKeys.DailyRewardsStatus] = new DailyRewardsPlayerStatus { StartEpochTime = nowMs },
        };
    }
}
