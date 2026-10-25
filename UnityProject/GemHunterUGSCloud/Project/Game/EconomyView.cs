using System.Collections.Generic;
using System.Linq;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Models;

namespace GemHunterUGSCloud.Game;

/// <summary>The client contract, unchanged since the Economy version.</summary>
public static class EconomyView
{
    /// <summary>
    /// Lists every catalog item, zeros included (StoreView indexes them directly). The infinite heart expiry
    /// stays in Unix seconds, as PlayerDataManager reads it.
    /// </summary>
    public static PlayerEconomyData From(WalletState wallet, WalletCatalog catalog) => new()
    {
        Currencies = catalog.Currencies.Keys.ToDictionary(id => id, id => checked((int)wallet.Currencies.GetValueOrDefault(id))),
        ItemInventory = catalog.Items.Keys.ToDictionary(id => id, id => wallet.Items.GetValueOrDefault(id)),
        HasPurchasedFreeCoinPack = wallet.Claims.Contains(GemHunterKeys.FreeCoinPackClaim),
        InfiniteHeartsExpiryTimestamp = wallet.TimedEffects.GetValueOrDefault(GemHunterKeys.InfiniteHeart),
    };
}
