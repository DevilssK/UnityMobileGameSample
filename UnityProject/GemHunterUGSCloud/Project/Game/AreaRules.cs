using System.Collections.Generic;
using System.Linq;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;

namespace GemHunterUGSCloud.Game;

/// <summary>
/// Area items: unlocked with stars, upgraded with coins. Invalid targets (unknown area or item, already
/// unlocked, locked, max level) leave the state unchanged and return false, as the original sample did.
/// </summary>
public static class AreaRules
{
    public static UpgradableAreaItem NewItem(int itemId, AreaItemDefinition definition, bool unlocked) => new()
    {
        UpgradableName = definition.Name,
        UpgradableId = itemId,
        IsUnlocked = unlocked,
        CurrentLevel = 0,
        MaxLevel = definition.MaxLevel,
        PerLevelCoinUpgradeRequirement = definition.CoinCostPerLevel,
    };

    public static bool Unlock(PlayerData player, int areaId, int itemId, GemHunterTuning tuning)
    {
        if (!TryGetArea(player, areaId, out var area))
        {
            return false;
        }

        EnsureItemExists(area, itemId, tuning);
        var item = area.UpgradableAreaItems.FirstOrDefault(i => i.UpgradableId == itemId);
        if (item == null || item.IsUnlocked)
        {
            return false;
        }

        if (player.Stars < area.UnlockRequirement_Stars)
        {
            throw new GameOperationException(ErrorCodes.InsufficientFunds, $"need {area.UnlockRequirement_Stars} stars, have {player.Stars}");
        }

        item.IsUnlocked = true;
        player.Stars -= area.UnlockRequirement_Stars;
        Touch(player, area);
        return true;
    }

    public static bool Upgrade(PlayerData player, WalletState wallet, int areaId, int itemId, GemHunterTuning tuning)
    {
        if (!TryGetArea(player, areaId, out var area))
        {
            return false;
        }

        var item = area.UpgradableAreaItems.FirstOrDefault(i => i.UpgradableId == itemId);
        if (item == null || !item.IsUnlocked || item.CurrentLevel >= item.MaxLevel)
        {
            return false;
        }

        WalletRules.Spend(wallet, GemHunterKeys.Coin, item.PerLevelCoinUpgradeRequirement);
        item.CurrentLevel++;
        Touch(player, area);
        return true;
    }

    /// <summary>
    /// Grants the current area's completion reward once. Completion is derived from the server state, not
    /// from the command list the client sends.
    /// </summary>
    public static IReadOnlyList<Grant> ClaimCompletion(PlayerData player, WalletState wallet, GemHunterTuning tuning, long nowUnixSec)
    {
        var areaId = player.CurrentArea?.AreaLevel ?? 1;
        if (!TryGetArea(player, areaId, out var area) || !IsComplete(area))
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"area {areaId} not complete");
        }

        if (area.CompletionRewardClaimed)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"area {areaId} reward already claimed");
        }

        if (!tuning.AreaCompleteRewards.TryGetValue($"area{areaId}CompleteRewards", out var rewards))
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"no completion reward for area {areaId}");
        }

        WalletRules.Credit(wallet, rewards, tuning.Wallet, nowUnixSec);
        area.CompletionRewardClaimed = true;
        Touch(player, area);
        return rewards;
    }

    private static bool IsComplete(AreaData area) =>
        area.UpgradableAreaItems.Count >= area.TotalUpgradableSlots
        && area.UpgradableAreaItems.All(i => i.IsUnlocked && i.CurrentLevel >= i.MaxLevel);

    private static bool TryGetArea(PlayerData player, int areaId, out AreaData area)
    {
        area = player.GameAreasData?.FirstOrDefault(a => a.AreaLevel == areaId)!;
        return area != null;
    }

    private static void EnsureItemExists(AreaData area, int itemId, GemHunterTuning tuning)
    {
        if (area.UpgradableAreaItems.Count >= area.TotalUpgradableSlots
            || area.UpgradableAreaItems.Any(i => i.UpgradableId == itemId)
            || !tuning.AreaItems.TryGetValue(itemId, out var definition))
        {
            return;
        }

        area.UpgradableAreaItems.Add(NewItem(itemId, definition, unlocked: false));
        area.UpgradableAreaItems = area.UpgradableAreaItems.OrderBy(i => i.UpgradableId).ToList();
    }

    private static void Touch(PlayerData player, AreaData area)
    {
        area.CurrentProgress = area.UpgradableAreaItems.Sum(i => i.IsUnlocked ? i.CurrentLevel : 0);
        player.CurrentArea = area;
    }
}
