using DevilCorp.MobileFoundation.Server.Wallet;

namespace GemHunterUGSCloud.Game;

public static class FreeCoinPackRules
{
    /// <summary>Grants the free pack once per player. False means it was already claimed: nothing changes.</summary>
    public static bool Claim(WalletState wallet, GemHunterTuning tuning, long nowUnixSec)
    {
        if (!WalletRules.TryClaim(wallet, GemHunterKeys.FreeCoinPackClaim))
        {
            return false;
        }

        WalletRules.Credit(wallet, tuning.FreeCoinPack, tuning.Wallet, nowUnixSec);
        return true;
    }
}
