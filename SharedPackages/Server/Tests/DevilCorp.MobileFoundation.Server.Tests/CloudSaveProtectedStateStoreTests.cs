using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.CloudSave.Model;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class CloudSaveProtectedStateStoreTests
{
    private readonly IGameApiClient _api = Substitute.For<IGameApiClient>();
    private readonly ICloudSaveDataApi _cloudSave = Substitute.For<ICloudSaveDataApi>();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();

    public CloudSaveProtectedStateStoreTests()
    {
        _api.CloudSaveData.Returns(_cloudSave);
        _context.ServiceToken.Returns("service-token");
        _context.AccessToken.Returns("player-token");
        _context.ProjectId.Returns("project");
        _context.PlayerId.Returns("player");
    }

    [Fact]
    public async Task WriteBatchAsync_MoreThan20Writes_Throws()
    {
        var store = new CloudSaveProtectedStateStore(_api);
        var writes = Enumerable.Range(0, 21).Select(i => new ItemWrite($"K{i}", new JObject(), null)).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => store.TryWriteBatchAsync(_context, writes, CancellationToken.None));

        await _cloudSave.DidNotReceiveWithAnyArgs().SetProtectedItemBatchAsync(default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task TryWriteBatchAsync_Conflict409_ReturnsFalse()
    {
        _cloudSave.SetProtectedItemBatchAsync(_context, "service-token", "project", "player", Arg.Any<SetItemBatchBody>(), Arg.Any<CancellationToken>())
            .Returns<Task<ApiResponse<SetItemBatchResponse>>>(_ => throw new ApiException(ApiExceptionType.Http, "conflict",
                Response<object>(HttpStatusCode.Conflict, null)));
        var store = new CloudSaveProtectedStateStore(_api);

        var written = await store.TryWriteBatchAsync(_context, new[] { new ItemWrite("WALLET", new JObject(), "lock-1") }, CancellationToken.None);

        Assert.False(written);
    }

    [Fact]
    public async Task TryWriteBatchAsync_OtherHttpError_ThrowsRetryLater()
    {
        _cloudSave.SetProtectedItemBatchAsync(_context, "service-token", "project", "player", Arg.Any<SetItemBatchBody>(), Arg.Any<CancellationToken>())
            .Returns<Task<ApiResponse<SetItemBatchResponse>>>(_ => throw new ApiException(ApiExceptionType.Http, "boom",
                Response<object>(HttpStatusCode.InternalServerError, null)));
        var store = new CloudSaveProtectedStateStore(_api);

        var error = await Assert.ThrowsAsync<GameOperationException>(() =>
            store.TryWriteBatchAsync(_context, new[] { new ItemWrite("WALLET", new JObject(), null) }, CancellationToken.None));

        Assert.Equal(ErrorCodes.RetryLater, error.Code);
    }

    [Fact]
    public async Task ReadAsync_NetworkFailure_ThrowsRetryLater()
    {
        _cloudSave.GetProtectedItemsAsync(_context, "service-token", "project", "player", Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<ApiResponse<GetItemsResponse>>>(_ => throw new System.Net.Http.HttpRequestException("unreachable"));
        var store = new CloudSaveProtectedStateStore(_api);

        var error = await Assert.ThrowsAsync<GameOperationException>(() => store.ReadAsync(_context, new[] { "WALLET" }, CancellationToken.None));

        Assert.Equal(ErrorCodes.RetryLater, error.Code);
    }

    [Fact]
    public async Task TryWriteBatchAsync_SendsServiceTokenAndWriteLocks()
    {
        SetItemBatchBody? sent = null;
        _cloudSave.SetProtectedItemBatchAsync(_context, "service-token", "project", "player", Arg.Do<SetItemBatchBody>(b => sent = b), Arg.Any<CancellationToken>())
            .Returns(Response(HttpStatusCode.OK, new SetItemBatchResponse(new List<SetItemBatchResponseResultsInner>())));
        var store = new CloudSaveProtectedStateStore(_api);

        var written = await store.TryWriteBatchAsync(_context, new[]
        {
            new ItemWrite("WALLET", JObject.FromObject(new { coins = 5 }), "lock-1"),
            new ItemWrite("PURCHASE_X", new JObject(), null),
        }, CancellationToken.None);

        Assert.True(written);
        Assert.NotNull(sent);
        Assert.Equal(new[] { "WALLET", "PURCHASE_X" }, sent!.Data.Select(d => d.Key));
        Assert.Equal("lock-1", sent.Data[0].WriteLock);
        Assert.Null(sent.Data[1].WriteLock);
    }

    [Fact]
    public async Task ReadAsync_ValueObject_RoundTripsAsJToken()
    {
        // The spike showed Item.Value arrives as a Newtonsoft JObject.
        var stored = JObject.FromObject(new { coins = 7, items = new { BOMB = 2 } });
        _cloudSave.GetProtectedItemsAsync(_context, "service-token", "project", "player",
                Arg.Is<List<string>>(k => k.SequenceEqual(new[] { "WALLET", "MISSING" })), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Response(HttpStatusCode.OK, new GetItemsResponse(
                new List<Item> { new("WALLET", stored, "lock-9", new ModifiedMetadata(DateTime.UtcNow), new ModifiedMetadata(DateTime.UtcNow)) }, new GetItemsResponseLinks(""))));
        var store = new CloudSaveProtectedStateStore(_api);

        var items = await store.ReadAsync(_context, new[] { "WALLET", "MISSING" }, CancellationToken.None);

        var wallet = Assert.Single(items).Value;
        Assert.Equal("lock-9", wallet.WriteLock);
        Assert.Equal(7, wallet.Value["coins"]!.ToObject<int>());
        Assert.Equal(2, wallet.Value["items"]!["BOMB"]!.ToObject<int>());
    }

    // ApiResponse<T> exposes read-only properties; tests set them by reflection to build canned responses.
    private static ApiResponse<T> Response<T>(HttpStatusCode statusCode, T? data)
    {
        var response = new ApiResponse<T>();
        Set(response, nameof(ApiResponse<T>.StatusCode), statusCode);
        Set(response, nameof(ApiResponse<T>.Data), data);
        return response;
    }

    private static void Set(object target, string propertyName, object? value)
    {
        var setter = target.GetType().GetProperty(propertyName)!.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(target, new[] { value });
            return;
        }

        target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);
    }
}
