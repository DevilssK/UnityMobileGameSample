using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GemHunterUGSCloud.Services;
using GemHunterUGSCloud.Tests.Support;
using Xunit;

namespace GemHunterUGSCloud.Tests.Services;

public class GameFunctionsTests
{
    private readonly GameTestHarness _harness = new();

    private PlayerDataService PlayerData() => new(GameTestHarness.Logger<PlayerDataService>(), _harness.State);
    private UpgradableAreaItemsService Areas() => new(GameTestHarness.Logger<UpgradableAreaItemsService>(), _harness.State);
    private DailyRewardsClaimService DailyClaim() => new(GameTestHarness.Logger<DailyRewardsClaimService>(), _harness.State);
    private DailyRewardsStatusService DailyStatus() => new(GameTestHarness.Logger<DailyRewardsStatusService>(), _harness.State);

    [Fact]
    public async Task OnSignIn_NewThenExistingPlayer_CreatesStateOnce()
    {
        var context = GameTestHarness.Context();

        var first = await PlayerData().OnSignInHandlePlayerInitialization(context);
        var second = await PlayerData().OnSignInHandlePlayerInitialization(context);

        Assert.True(first.IsNewPlayer);
        Assert.False(second.IsNewPlayer);
        Assert.Single(_harness.Store.Batches);
        Assert.Equal(200, second.EconomyData.Currencies["COIN"]);
        Assert.Equal(5, second.EconomyData.ItemInventory.Count);
        Assert.Equal(8, second.PlayerData.Hearts);
    }

    [Fact]
    public async Task GetPlayerEconomyData_DuringGracePeriod_IsReadable()
    {
        var context = GameTestHarness.Context();

        var economy = await PlayerData().GetPlayerEconomyData(context);

        Assert.Equal(200, economy!.Currencies["COIN"]);
    }

    [Fact]
    public async Task HandleLevelWon_SameOperationTwice_AddsOneStar()
    {
        var context = await _harness.NewPlayerAsync();

        await PlayerData().HandleLevelWon(context, "op-1");
        var player = await PlayerData().HandleLevelWon(context, "op-1");

        Assert.Equal(6, player!.Stars);
    }

    [Fact]
    public async Task HandleLevelLost_RemovesHeart()
    {
        var context = await _harness.NewPlayerAsync();

        var player = await PlayerData().HandleLevelLost(context, "op-1");

        Assert.Equal(7, player!.Hearts);
    }

    [Fact]
    public async Task HandleUpgrade_SameOperationTwice_SpendsOnce()
    {
        var context = await _harness.NewPlayerAsync();

        await Areas().HandleUpgrade(context, 1, 1, "op-up");
        var player = await Areas().HandleUpgrade(context, 1, 1, "op-up");
        var economy = await PlayerData().GetPlayerEconomyData(context);

        Assert.Equal(1, player.GameAreasData!.Single().UpgradableAreaItems.Single().CurrentLevel);
        Assert.Equal(180, economy!.Currencies["COIN"]);
    }

    [Fact]
    public async Task HandleUnlock_UsesStars()
    {
        var context = await _harness.NewPlayerAsync();

        var player = await Areas().HandleUnlock(context, 1, 2, "op-unlock");

        Assert.Equal(4, player.Stars);
    }

    [Fact]
    public async Task ProcessAreaComplete_IncompleteArea_IsRefused()
    {
        var context = await _harness.NewPlayerAsync();
        var batching = new CommandBatchingService_AreaUpgradables(GameTestHarness.Logger<CommandBatchingService_AreaUpgradables>(), _harness.State);

        var error = await Assert.ThrowsAnyAsync<System.Exception>(() =>
            batching.ProcessAreaCompleteCommandBatch(context, new List<string> { "AreaCompleteCommand" }, "op-area"));

        Assert.StartsWith("CONFIGURATION_ERROR", error.Message);
    }

    [Fact]
    public async Task ClaimDailyReward_Twice_SecondIsRefusedWithoutGrant()
    {
        var context = await _harness.NewPlayerAsync();

        var claimed = await DailyClaim().ClaimDailyReward(context);
        var error = await Assert.ThrowsAnyAsync<System.Exception>(() => DailyClaim().ClaimDailyReward(context));
        var status = await DailyStatus().GetDailyRewardsStatus(context);
        var economy = await PlayerData().GetPlayerEconomyData(context);

        Assert.Equal(1, claimed.DaysClaimed);
        Assert.StartsWith("RETRY_LATER", error.Message);
        Assert.Equal(1, status.DaysClaimed);
        Assert.Equal(210, economy!.Currencies["COIN"]);
    }
}
