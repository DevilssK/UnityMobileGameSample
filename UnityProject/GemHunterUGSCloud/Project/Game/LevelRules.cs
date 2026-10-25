using System;
using System.Collections.Generic;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;

namespace GemHunterUGSCloud.Game;

/// <summary>
/// Level results are declared by the client: the server cannot prove a win. The caps bound the abuse;
/// nothing sold depends on these values.
/// </summary>
public static class LevelRules
{
    public const int MaxStars = 25;

    public static void Won(PlayerData player)
    {
        player.Stars = Math.Min(MaxStars, player.Stars + 1);
    }

    public static void Lost(PlayerData player, WalletState wallet, long nowUnixSec)
    {
        if (wallet.TimedEffects.GetValueOrDefault(GemHunterKeys.InfiniteHeart) > nowUnixSec)
        {
            return;
        }

        player.Hearts = Math.Max(0, player.Hearts - 1);
    }
}
