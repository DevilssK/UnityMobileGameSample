using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

/// <summary>Claims today's reward on the server clock; the claim and the credit are one atomic write.</summary>
public class DailyRewardsClaimService
{
    private readonly ILogger<DailyRewardsClaimService> m_Logger;
    private readonly GemHunterState m_State;

    public DailyRewardsClaimService(ILogger<DailyRewardsClaimService> logger, GemHunterState state)
    {
        m_Logger = logger;
        m_State = state;
    }

    [CloudCodeFunction("ClaimDailyReward")]
    public async Task<DailyRewardsResult> ClaimDailyReward(IExecutionContext context)
    {
        var result = await m_State.RunAsync(context, new[] { GemHunterKeys.DailyRewardsStatus }, (snapshot, tuning) =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            var nowMs = m_State.Clock.UtcNowUnixMs;
            if (!snapshot.TryGet<DailyRewardsPlayerStatus>(GemHunterKeys.DailyRewardsStatus, out var status))
            {
                status = new DailyRewardsPlayerStatus { StartEpochTime = nowMs };
            }

            var claimed = DailyRewardRules.Claim(status, tuning.DailyRewards, wallet, tuning.Wallet, nowMs);
            return DevilCorp.MobileFoundation.Server.State.StateMutation.Write(claimed,
                (GemHunterKeys.DailyRewardsStatus, (object)status), (WalletState.Key, wallet));
        }, CancellationToken.None);

        m_Logger.LogInformation("Daily reward claimed (day {Day}, correlation {CorrelationId})", result.DaysClaimed, context.CorrelationId);
        return result;
    }
}
