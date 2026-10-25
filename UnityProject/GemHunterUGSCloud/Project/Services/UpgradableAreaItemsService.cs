using System;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

/// <summary>Area items: unlock with stars, upgrade with coins, both in one atomic write with the wallet.</summary>
public class UpgradableAreaItemsService
{
    private readonly ILogger<UpgradableAreaItemsService> m_Logger;
    private readonly GemHunterState m_State;

    public UpgradableAreaItemsService(ILogger<UpgradableAreaItemsService> logger, GemHunterState state)
    {
        m_Logger = logger;
        m_State = state;
    }

    [CloudCodeFunction("HandleUnlock")]
    public Task<PlayerData> HandleUnlock(IExecutionContext context, int areaId, int itemId, string operationId) =>
        Apply(context, operationId, "unlock", (player, _, tuning) => AreaRules.Unlock(player, areaId, itemId, tuning));

    [CloudCodeFunction("HandleUpgrade")]
    public Task<PlayerData> HandleUpgrade(IExecutionContext context, int areaId, int itemId, string operationId) =>
        Apply(context, operationId, "upgrade", (player, wallet, tuning) => AreaRules.Upgrade(player, wallet, areaId, itemId, tuning));

    private async Task<PlayerData> Apply(IExecutionContext context, string operationId, string operation,
        Func<PlayerData, WalletState, GemHunterTuning, bool> rule)
    {
        var player = await m_State.RunAsync(context, new[] { GemHunterKeys.PlayerData }, (snapshot, tuning) =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            if (!snapshot.TryGet<PlayerData>(GemHunterKeys.PlayerData, out var playerData))
            {
                throw new GameOperationException(ErrorCodes.RetryLater, "player data not readable yet");
            }

            if (!WalletRules.TryRecordOperation(wallet, operationId) || !rule(playerData, wallet, tuning))
            {
                // Already applied, or an invalid target: return the current state without writing.
                return StateMutation.Read(playerData);
            }

            return StateMutation.Write(playerData, (GemHunterKeys.PlayerData, (object)playerData), (WalletState.Key, wallet));
        }, CancellationToken.None);

        m_Logger.LogInformation("Area {Operation} handled (operation {OperationId}, correlation {CorrelationId})",
            operation, operationId, context.CorrelationId);
        return player;
    }
}
