using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

/// <summary>Player lifecycle, level results and read access to the protected state.</summary>
public class PlayerDataService
{
    private readonly ILogger<PlayerDataService> m_Logger;
    private readonly GemHunterState m_State;

    public PlayerDataService(ILogger<PlayerDataService> logger, GemHunterState state)
    {
        m_Logger = logger;
        m_State = state;
    }

    /// <summary>
    /// Called right after sign-in: creates the protected state of a new player, so that the creation grace
    /// period runs while the player is still in the menus.
    /// </summary>
    [CloudCodeFunction("OnSignInHandlePlayerInitialization")]
    public async Task<PlayerInitializationResponse> OnSignInHandlePlayerInitialization(IExecutionContext context)
    {
        var created = await m_State.EnsureInitializedAsync(context, CancellationToken.None);
        var snapshot = await m_State.ReadAsync(context, CancellationToken.None);
        m_Logger.LogInformation("Player initialization (new player: {IsNewPlayer}, correlation {CorrelationId})", created, context.CorrelationId);

        return new PlayerInitializationResponse
        {
            PlayerData = WithEffects(snapshot.Player, snapshot.Wallet),
            EconomyData = EconomyView.From(snapshot.Wallet, snapshot.Tuning.Wallet),
            IsNewPlayer = created,
            InitializationTimestamp = m_State.NowUnixSec,
        };
    }

    [CloudCodeFunction("GetPlayerData")]
    public async Task<PlayerData?> GetPlayerData(IExecutionContext context)
    {
        var snapshot = await m_State.ReadAsync(context, CancellationToken.None);
        return WithEffects(snapshot.Player, snapshot.Wallet);
    }

    [CloudCodeFunction("GetPlayerEconomyData")]
    public async Task<PlayerEconomyData?> GetPlayerEconomyData(IExecutionContext context)
    {
        var snapshot = await m_State.ReadAsync(context, CancellationToken.None);
        return EconomyView.From(snapshot.Wallet, snapshot.Tuning.Wallet);
    }

    /// <summary>Declared by the client; see <see cref="LevelRules"/> for why this is bounded, not proven.</summary>
    [CloudCodeFunction("HandleLevelWon")]
    public Task<PlayerData?> HandleLevelWon(IExecutionContext context, string operationId) =>
        ApplyLevelResult(context, operationId, (player, _) => LevelRules.Won(player));

    [CloudCodeFunction("HandleLevelLost")]
    public Task<PlayerData?> HandleLevelLost(IExecutionContext context, string operationId) =>
        ApplyLevelResult(context, operationId, (player, wallet) => LevelRules.Lost(player, wallet, m_State.NowUnixSec));

    private async Task<PlayerData?> ApplyLevelResult(IExecutionContext context, string operationId, System.Action<PlayerData, WalletState> rule)
    {
        var player = await m_State.RunAsync(context, new[] { GemHunterKeys.PlayerData }, (snapshot, _) =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            if (!snapshot.TryGet<PlayerData>(GemHunterKeys.PlayerData, out var playerData))
            {
                throw new GameOperationException(ErrorCodes.RetryLater, "player data not readable yet");
            }

            if (!WalletRules.TryRecordOperation(wallet, operationId))
            {
                return StateMutation.Read(WithEffects(playerData, wallet));
            }

            rule(playerData, wallet);
            return StateMutation.Write(WithEffects(playerData, wallet),
                (GemHunterKeys.PlayerData, (object)playerData), (WalletState.Key, wallet));
        }, CancellationToken.None);

        m_Logger.LogInformation("Level result applied (operation {OperationId}, correlation {CorrelationId})", operationId, context.CorrelationId);
        return player;
    }

    private PlayerData WithEffects(PlayerData player, WalletState wallet)
    {
        player.HasInfiniteHeartEffectActive = wallet.TimedEffects.GetValueOrDefault(GemHunterKeys.InfiniteHeart) > m_State.NowUnixSec;
        return player;
    }
}
