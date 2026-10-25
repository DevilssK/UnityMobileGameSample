using System.Linq;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Xunit;

namespace GemHunterUGSCloud.Tests.Game;

public class GameRulesTests
{
    private readonly GemHunterTuning _tuning = TestTuning.Create();

    private (WalletState Wallet, PlayerData Player, DailyRewardsPlayerStatus Daily) NewPlayer()
    {
        var state = GemHunterInitialState.Build(_tuning, TestTuning.NowMs);
        return ((WalletState)state[WalletState.Key], (PlayerData)state[GemHunterKeys.PlayerData],
            (DailyRewardsPlayerStatus)state[GemHunterKeys.DailyRewardsStatus]);
    }

    [Fact]
    public void InitialState_MatchesPreviousEconomyValues()
    {
        var (wallet, player, daily) = NewPlayer();

        Assert.Equal(TestTuning.NowMs, wallet.CreatedAtUnixMs);
        Assert.Equal(200, wallet.Currencies["COIN"]);
        Assert.Equal(3, wallet.Items["SMALL_BOMB"]);
        Assert.Equal(1, wallet.Items["COLOR_BONUS"]);
        Assert.Equal(8, player.Hearts);
        Assert.Equal(5, player.Stars);
        var shark = Assert.Single(player.CurrentArea!.UpgradableAreaItems);
        Assert.True(shark.IsUnlocked);
        Assert.Equal(20, shark.PerLevelCoinUpgradeRequirement);
        Assert.Equal(TestTuning.NowMs, daily.StartEpochTime);
    }

    [Fact]
    public void Won_AddsStarUpTo25()
    {
        var (_, player, _) = NewPlayer();
        LevelRules.Won(player);
        Assert.Equal(6, player.Stars);

        player.Stars = 25;
        LevelRules.Won(player);
        Assert.Equal(25, player.Stars);
    }

    [Fact]
    public void Lost_RemovesHeartDownToZero()
    {
        var (wallet, player, _) = NewPlayer();
        LevelRules.Lost(player, wallet, TestTuning.NowSec);
        Assert.Equal(7, player.Hearts);

        player.Hearts = 0;
        LevelRules.Lost(player, wallet, TestTuning.NowSec);
        Assert.Equal(0, player.Hearts);
    }

    [Fact]
    public void Lost_WithActiveInfiniteHeart_KeepsHearts()
    {
        var (wallet, player, _) = NewPlayer();
        wallet.TimedEffects[GemHunterKeys.InfiniteHeart] = TestTuning.NowSec + 10;

        LevelRules.Lost(player, wallet, TestTuning.NowSec);

        Assert.Equal(8, player.Hearts);
    }

    [Fact]
    public void Lost_WithExpiredInfiniteHeart_RemovesHeart()
    {
        var (wallet, player, _) = NewPlayer();
        wallet.TimedEffects[GemHunterKeys.InfiniteHeart] = TestTuning.NowSec;

        LevelRules.Lost(player, wallet, TestTuning.NowSec);

        Assert.Equal(7, player.Hearts);
    }

    [Fact]
    public void Unlock_NewItem_SpendsStarsAndAddsItemFromTuning()
    {
        var (_, player, _) = NewPlayer();

        var changed = AreaRules.Unlock(player, 1, 2, _tuning);

        Assert.True(changed);
        Assert.Equal(4, player.Stars);
        var seahorse = player.GameAreasData!.Single().UpgradableAreaItems.Single(i => i.UpgradableId == 2);
        Assert.True(seahorse.IsUnlocked);
        Assert.Equal(40, seahorse.PerLevelCoinUpgradeRequirement);
    }

    [Fact]
    public void Unlock_WithoutStars_ThrowsInsufficientFunds()
    {
        var (_, player, _) = NewPlayer();
        player.Stars = 0;

        var error = Assert.Throws<GameOperationException>(() => AreaRules.Unlock(player, 1, 2, _tuning));

        Assert.Equal(ErrorCodes.InsufficientFunds, error.Code);
    }

    [Fact]
    public void Unlock_UnknownAreaOrAlreadyUnlocked_ReturnsUnchanged()
    {
        var (_, player, _) = NewPlayer();

        Assert.False(AreaRules.Unlock(player, 9, 2, _tuning));
        Assert.False(AreaRules.Unlock(player, 1, 1, _tuning));
        Assert.Equal(5, player.Stars);
    }

    [Fact]
    public void Upgrade_SpendsCoinsAndRaisesLevel()
    {
        var (wallet, player, _) = NewPlayer();

        var changed = AreaRules.Upgrade(player, wallet, 1, 1, _tuning);

        Assert.True(changed);
        Assert.Equal(180, wallet.Currencies["COIN"]);
        Assert.Equal(1, player.GameAreasData!.Single().UpgradableAreaItems.Single().CurrentLevel);
        Assert.Equal(1, player.GameAreasData!.Single().CurrentProgress);
    }

    [Fact]
    public void Upgrade_WithoutCoins_ThrowsInsufficientFunds()
    {
        var (wallet, player, _) = NewPlayer();
        wallet.Currencies["COIN"] = 19;

        var error = Assert.Throws<GameOperationException>(() => AreaRules.Upgrade(player, wallet, 1, 1, _tuning));

        Assert.Equal(ErrorCodes.InsufficientFunds, error.Code);
        Assert.Equal(0, player.GameAreasData!.Single().UpgradableAreaItems.Single().CurrentLevel);
    }

    [Fact]
    public void Upgrade_AtMaxLevel_ReturnsUnchanged()
    {
        var (wallet, player, _) = NewPlayer();
        player.GameAreasData!.Single().UpgradableAreaItems.Single().CurrentLevel = 5;

        Assert.False(AreaRules.Upgrade(player, wallet, 1, 1, _tuning));
        Assert.Equal(200, wallet.Currencies["COIN"]);
    }

    [Fact]
    public void ClaimCompletion_IncompleteArea_Throws()
    {
        var (wallet, player, _) = NewPlayer();

        Assert.Throws<GameOperationException>(() => AreaRules.ClaimCompletion(player, wallet, _tuning, TestTuning.NowSec));
    }

    [Fact]
    public void ClaimCompletion_CompleteArea_GrantsOnce()
    {
        var (wallet, player, _) = NewPlayer();
        CompleteFirstArea(player);

        var rewards = AreaRules.ClaimCompletion(player, wallet, _tuning, TestTuning.NowSec);

        Assert.Equal(3, rewards.Count);
        Assert.Equal(5, wallet.Items["COLOR_BONUS"]);
        Assert.True(player.GameAreasData!.Single().CompletionRewardClaimed);
        Assert.Throws<GameOperationException>(() => AreaRules.ClaimCompletion(player, wallet, _tuning, TestTuning.NowSec));
        Assert.Equal(5, wallet.Items["COLOR_BONUS"]);
    }

    [Fact]
    public void FreeCoinPack_ClaimedTwice_GrantsOnce()
    {
        var (wallet, _, _) = NewPlayer();

        Assert.True(FreeCoinPackRules.Claim(wallet, _tuning, TestTuning.NowSec));
        Assert.False(FreeCoinPackRules.Claim(wallet, _tuning, TestTuning.NowSec));

        Assert.Equal(210, wallet.Currencies["COIN"]);
    }

    [Fact]
    public void EconomyView_IncludesZeroItemsAndSecondsExpiry()
    {
        var (wallet, _, _) = NewPlayer();
        wallet.Items["SMALL_BOMB"] = 0;
        wallet.TimedEffects[GemHunterKeys.InfiniteHeart] = TestTuning.NowSec + 360;
        FreeCoinPackRules.Claim(wallet, _tuning, TestTuning.NowSec);

        var view = EconomyView.From(wallet, _tuning.Wallet);

        Assert.Equal(0, view.ItemInventory["SMALL_BOMB"]);
        Assert.Equal(5, view.ItemInventory.Count);
        Assert.Equal(210, view.Currencies["COIN"]);
        Assert.Equal(TestTuning.NowSec + 360, view.InfiniteHeartsExpiryTimestamp);
        Assert.True(view.HasPurchasedFreeCoinPack);
    }

    private void CompleteFirstArea(PlayerData player)
    {
        var area = player.GameAreasData!.Single();
        foreach (var id in Enumerable.Range(2, 4))
        {
            player.Stars = 5;
            AreaRules.Unlock(player, 1, id, _tuning);
        }

        foreach (var item in area.UpgradableAreaItems)
        {
            item.CurrentLevel = item.MaxLevel;
        }
    }
}
