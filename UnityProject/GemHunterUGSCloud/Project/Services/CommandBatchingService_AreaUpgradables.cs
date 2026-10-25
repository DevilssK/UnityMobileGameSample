using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

/// <summary>
/// Area completion reward. The sample trusted the command list sent by the client; completion is now
/// derived from the protected area state, and the list is only logged.
/// </summary>
public class CommandBatchingService_AreaUpgradables
{
    private readonly ILogger<CommandBatchingService_AreaUpgradables> m_Logger;
    private readonly GemHunterState m_State;

    public CommandBatchingService_AreaUpgradables(ILogger<CommandBatchingService_AreaUpgradables> logger, GemHunterState state)
    {
        m_Logger = logger;
        m_State = state;
    }

    [CloudCodeFunction("ProcessAreaCompleteCommandBatch")]
    public async Task<CommandReward> ProcessAreaCompleteCommandBatch(IExecutionContext context, List<string> timeSignedCommandKeys, string operationId)
    {
        var rewards = await m_State.RunAsync(context, new[] { GemHunterKeys.PlayerData }, (snapshot, tuning) =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            if (!snapshot.TryGet<PlayerData>(GemHunterKeys.PlayerData, out var playerData))
            {
                throw new GameOperationException(ErrorCodes.RetryLater, "player data not readable yet");
            }

            if (!WalletRules.TryRecordOperation(wallet, operationId))
            {
                return StateMutation.Read<IReadOnlyList<Grant>>(new List<Grant>());
            }

            var granted = AreaRules.ClaimCompletion(playerData, wallet, tuning, m_State.NowUnixSec);
            return StateMutation.Write(granted, (GemHunterKeys.PlayerData, (object)playerData), (WalletState.Key, wallet));
        }, CancellationToken.None);

        m_Logger.LogInformation("Area completion handled ({CommandCount} client commands, operation {OperationId}, correlation {CorrelationId})",
            timeSignedCommandKeys?.Count ?? 0, operationId, context.CorrelationId);

        return new CommandReward
        {
            Rewards = rewards.Select(r => new Reward { Service = "inventory", Id = r.Id, Amount = (int)r.Amount }).ToList(),
        };
    }
}
