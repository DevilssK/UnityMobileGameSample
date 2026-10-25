using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using Xunit;

namespace GemHunterUGSCloud.Tests.Game;

public class DailyRewardRulesTests
{
    private const long DayMs = 86_400_000;
    private readonly GemHunterTuning _tuning = TestTuning.Create();
    private readonly WalletState _wallet;

    public DailyRewardRulesTests()
    {
        _wallet = WalletRules.Initial(_tuning.Wallet, 0);
    }

    private static DailyRewardsPlayerStatus Fresh() => new() { StartEpochTime = TestTuning.NowMs };

    [Fact]
    public void Status_FirstDay_IsClaimable()
    {
        var result = DailyRewardRules.Status(Fresh(), _tuning.DailyRewards, TestTuning.NowMs);

        Assert.True(result.IsStarted);
        Assert.False(result.IsEnded);
        Assert.Equal(0, result.SecondsTillClaimable);
        Assert.Equal(3, result.DaysRemaining);
    }

    [Fact]
    public void Claim_FirstDay_GrantsFirstRewardAndRecordsClaim()
    {
        var status = Fresh();

        var result = DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs);

        Assert.Equal(210, _wallet.Currencies["COIN"]);
        Assert.Equal(1, status.DaysClaimed);
        Assert.Equal(TestTuning.NowMs, status.LastClaimTime);
        Assert.Equal("COIN", Assert.Single(result.RewardsGranted).Id);
        Assert.Equal(1, result.DaysClaimed);
        Assert.Equal(86_400, result.SecondsTillClaimable);
    }

    [Fact]
    public void Claim_AlreadyClaimedToday_ThrowsWithoutGrant()
    {
        var status = Fresh();
        DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs);

        var error = Assert.Throws<GameOperationException>(() =>
            DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs + 1_000));

        Assert.Equal(ErrorCodes.RetryLater, error.Code);
        Assert.Equal(210, _wallet.Currencies["COIN"]);
        Assert.Equal(1, status.DaysClaimed);
    }

    [Fact]
    public void Claim_WithinFiveSecondsOfNextDay_IsAccepted()
    {
        var status = Fresh();
        DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs);

        DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs + DayMs - 4_000);

        Assert.Equal(230, _wallet.Currencies["COIN"]);
    }

    [Fact]
    public void Claim_AfterLastDay_ThrowsEnded()
    {
        var status = Fresh();
        for (var day = 0; day < 3; day++)
        {
            DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs + day * DayMs);
        }

        Assert.True(DailyRewardRules.Status(status, _tuning.DailyRewards, TestTuning.NowMs + 3 * DayMs).IsEnded);
        Assert.Throws<GameOperationException>(() =>
            DailyRewardRules.Claim(status, _tuning.DailyRewards, _wallet, _tuning.Wallet, TestTuning.NowMs + 3 * DayMs));
        Assert.Equal(260, _wallet.Currencies["COIN"]);
    }
}
