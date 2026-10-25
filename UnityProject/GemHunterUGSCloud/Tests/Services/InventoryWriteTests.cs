using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GemHunterUGSCloud.Services;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Economy.Api;
using Unity.Services.Economy.Model;
using Xunit;
using static GemHunterUGSCloud.Tests.Support.PurchaseTestDoubles;

namespace GemHunterUGSCloud.Tests.Services;

public class InventoryWriteTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static InventoryResponse Instance(string id, string itemId, string? instanceDataJson) =>
        new(id, itemId, instanceDataJson == null ? null : JObject.Parse(instanceDataJson), "lock",
            new ModifiedMetadata(Created), new ModifiedMetadata(Created));

    private static ApiResponse<PlayerInventoryResponse> Page(string? next, params InventoryResponse[] instances)
    {
        var links = (PlayerCurrencyBalanceResponseLinks)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(PlayerCurrencyBalanceResponseLinks));
        typeof(PlayerCurrencyBalanceResponseLinks).GetProperty("Next")!.GetSetMethod(true)!.Invoke(links, new object?[] { next });
        return Response(HttpStatusCode.OK, new PlayerInventoryResponse(new List<InventoryResponse>(instances), links));
    }

    private static Task<ApiResponse<PlayerInventoryResponse>> GetInventory(IEconomyInventoryApi inventory) =>
        inventory.GetPlayerInventoryAsync(
            Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(),
            Arg.Any<List<string>>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>());

    private static (PlayerEconomyService Economy, IEconomyInventoryApi Inventory) Create()
    {
        var (client, _) = Client();
        var inventory = Substitute.For<IEconomyInventoryApi>();
        client.EconomyInventory.Returns(inventory);
        return (new PlayerEconomyService(Logger<PlayerEconomyService>(), client), inventory);
    }

    [Fact]
    public async Task UpdateInventoryItem_WhenRedeemedUnitListedFirst_AddsToCounterInstance()
    {
        var (economy, inventory) = Create();
        GetInventory(inventory).Returns(Page(null,
            Instance("unit", "LARGE_BOMB", null),
            Instance("counter", "LARGE_BOMB", "{\"amount\":3}")));

        await economy.UpdateInventoryItem(Context(), "LARGE_BOMB", 2);

        await inventory.Received(1).UpdateInventoryItemAsync(
            Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            "counter",
            Arg.Is<InventoryRequestUpdate>(r => JObject.FromObject(r.InstanceData).Value<int>("amount") == 5),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateInventoryItem_WhenOnlyRedeemedUnits_CreatesCounterWithoutTouchingUnits()
    {
        var (economy, inventory) = Create();
        GetInventory(inventory).Returns(Page(null, Instance("unit", "LARGE_BOMB", null)));

        await economy.UpdateInventoryItem(Context(), "LARGE_BOMB", 2);

        await inventory.DidNotReceiveWithAnyArgs().UpdateInventoryItemAsync(
            default!, default!, default!, default!, default!, default!, default, default, default, default);
        await inventory.Received(1).AddInventoryItemAsync(
            Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<AddInventoryRequest>(r => r.InventoryItemId == "LARGE_BOMB"),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInventoryInstancesAsync_WhenSeveralPages_ReadsAllPages()
    {
        var (economy, inventory) = Create();
        GetInventory(inventory).Returns(
            Page("/next", Instance("a", "LARGE_BOMB", null)),
            Page(null, Instance("b", "LARGE_BOMB", null)));

        var instances = await economy.GetInventoryInstancesAsync(Context(), null);

        Assert.Equal(new[] { "a", "b" }, instances.ConvertAll(i => i.PlayersInventoryItemId));
        await inventory.Received(1).GetPlayerInventoryAsync(
            Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), "a", Arg.Any<int?>(),
            Arg.Any<List<string>>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>());
    }
}
