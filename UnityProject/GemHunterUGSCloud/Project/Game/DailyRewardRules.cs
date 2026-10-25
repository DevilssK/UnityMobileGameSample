using System.Collections.Generic;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;

namespace GemHunterUGSCloud.Game;

/// <summary>Daily rewards on the server clock; port of the sample's status and claim services.</summary>
public static class DailyRewardRules
{
    /// <summary>A claim this close to the next day is accepted, to absorb client/server clock drift.</summary>
    public const long ClaimGracePeriodSeconds = 5;

    public static DailyRewardsResult Status(DailyRewardsPlayerStatus status, ConfigData config, long nowMs)
    {
        var secondsSinceLastClaim = status.LastClaimTime > 0 ? (nowMs - status.LastClaimTime) / 1000 : 0;
        var isFirstClaim = status.LastClaimTime == 0;
        var canClaim = isFirstClaim
            || (secondsSinceLastClaim >= config.SecondsPerDay && status.DaysClaimed < config.TotalDays);

        var result = new DailyRewardsResult
        {
            Success = true,
            ConfigData = config,
            IsStarted = true,
            DaysClaimed = status.DaysClaimed,
            SecondsTillClaimable = canClaim ? 0 : config.SecondsPerDay - secondsSinceLastClaim,
            DaysRemaining = config.TotalDays - status.DaysClaimed,
        };
        result.SecondsTillNextDay = result.SecondsTillClaimable;
        result.IsEnded = result.DaysRemaining <= 0;
        return result;
    }

    /// <summary>Grants the next day's reward and updates <paramref name="status"/>; returns the new status.</summary>
    public static DailyRewardsResult Claim(DailyRewardsPlayerStatus status, ConfigData config, WalletState wallet, WalletCatalog catalog, long nowMs)
    {
        var current = Status(status, config, nowMs);
        if (current.IsEnded)
        {
            throw new GameOperationException(ErrorCodes.RetryLater, "daily rewards ended");
        }

        if (current.SecondsTillClaimable > ClaimGracePeriodSeconds)
        {
            throw new GameOperationException(ErrorCodes.RetryLater, $"daily reward claimable in {current.SecondsTillClaimable} s");
        }

        if (status.DaysClaimed >= config.DailyRewards.Count)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"no daily reward for day {status.DaysClaimed + 1}");
        }

        var reward = config.DailyRewards[status.DaysClaimed];
        WalletRules.Credit(wallet, new[] { new Grant(reward.Id, reward.Quantity) }, catalog, nowMs / 1000);
        status.DaysClaimed++;
        status.LastClaimTime = nowMs;

        var result = Status(status, config, nowMs);
        result.RewardsGranted = new List<DailyReward> { reward };
        return result;
    }
}
