using System.Threading;
using System.Threading.Tasks;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

public class DailyRewardsStatusService
{
    private readonly ILogger<DailyRewardsStatusService> m_Logger;
    private readonly GemHunterState m_State;

    public DailyRewardsStatusService(ILogger<DailyRewardsStatusService> logger, GemHunterState state)
    {
        m_Logger = logger;
        m_State = state;
    }

    [CloudCodeFunction("GetDailyRewardsStatus")]
    public async Task<DailyRewardsResult> GetDailyRewardsStatus(IExecutionContext context)
    {
        var snapshot = await m_State.ReadAsync(context, CancellationToken.None);
        var status = DailyRewardRules.Status(snapshot.Daily, snapshot.Tuning.DailyRewards, m_State.Clock.UtcNowUnixMs);
        m_Logger.LogDebug("Daily rewards status: day {Day}, claimable in {Seconds} s", status.DaysClaimed, status.SecondsTillClaimable);
        return status;
    }
}
