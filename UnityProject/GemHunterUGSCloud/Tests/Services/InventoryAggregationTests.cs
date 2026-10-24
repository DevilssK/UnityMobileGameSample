using System;
using System.Collections.Generic;
using GemHunterUGSCloud.Services;
using Newtonsoft.Json.Linq;
using Unity.Services.Economy.Model;
using Xunit;

namespace GemHunterUGSCloud.Tests.Services;

/// <summary>
/// Economy redeem grants inventory items as instances without instance data, next to the game's own
/// "counter" instance that stores {"amount": N}. Reads must add them up; writes must target the counter.
/// </summary>
public class InventoryAggregationTests
{
    private static InventoryResponse Instance(string id, string itemId, string? instanceDataJson, DateTime? created = null) =>
        new(id, itemId, instanceDataJson == null ? null : JObject.Parse(instanceDataJson), "lock",
            new ModifiedMetadata(created ?? DefaultCreated), new ModifiedMetadata(created ?? DefaultCreated));

    private static readonly DateTime DefaultCreated = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SumInventory_WhenRedeemedUnitsSitNextToCounter_AddsThemUp()
    {
        var instances = new List<InventoryResponse>
        {
            Instance("a", "LARGE_BOMB", "{\"amount\":3}"),
            Instance("b", "LARGE_BOMB", null),
            Instance("c", "LARGE_BOMB", null),
            Instance("d", "COLOR_BONUS", null)
        };

        var totals = PlayerEconomyService.SumInventory(instances);

        Assert.Equal(5, totals["LARGE_BOMB"]);
        Assert.Equal(1, totals["COLOR_BONUS"]);
    }

    [Fact]
    public void SumInventory_WhenInstanceDataHasNoAmount_CountsZero()
    {
        var instances = new List<InventoryResponse> { Instance("h", "INFINITE_HEART", "{\"active\":true,\"activatedAt\":1}") };

        Assert.Equal(0, PlayerEconomyService.SumInventory(instances)["INFINITE_HEART"]);
    }

    [Fact]
    public void SelectCounterInstance_WhenRedeemedUnitComesFirst_ReturnsInstanceWithAmount()
    {
        var instances = new List<InventoryResponse>
        {
            Instance("b", "LARGE_BOMB", null),
            Instance("a", "LARGE_BOMB", "{\"amount\":3}")
        };

        Assert.Equal("a", PlayerEconomyService.SelectCounterInstance(instances)!.PlayersInventoryItemId);
    }

    [Fact]
    public void SelectCounterInstance_WhenOnlyRedeemedUnits_ReturnsNull()
    {
        var instances = new List<InventoryResponse> { Instance("b", "LARGE_BOMB", null) };

        Assert.Null(PlayerEconomyService.SelectCounterInstance(instances));
    }

    [Fact]
    public void SelectCurrentInfiniteHeart_WhenExpiredHeartListedFirst_ReturnsMostRecentlyActivated()
    {
        var instances = new List<InventoryResponse>
        {
            Instance("old", "INFINITE_HEART", "{\"active\":false,\"activatedAt\":100}"),
            Instance("new", "INFINITE_HEART", null, new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc))
        };

        Assert.Equal("new", PlayerEconomyService.SelectCurrentInfiniteHeart(instances)!.PlayersInventoryItemId);
    }

    [Fact]
    public void SelectCurrentInfiniteHeart_WhenNone_ReturnsNull()
    {
        Assert.Null(PlayerEconomyService.SelectCurrentInfiniteHeart(new List<InventoryResponse>()));
    }
}
