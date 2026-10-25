using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>
/// Cloud Save protected access class. Server writes need the service token: the player's own token is
/// refused (401, verified in development on 2026-10-25), which is what keeps the client read-only.
/// </summary>
public sealed class CloudSaveProtectedStateStore : IProtectedStateStore
{
    public const int MaxBatchSize = 20;

    private readonly IGameApiClient _api;

    public CloudSaveProtectedStateStore(IGameApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyDictionary<string, StoredItem>> ReadAsync(IExecutionContext context, IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        var response = await _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, context.PlayerId, keys.ToList(), null!, ct);

        return response.Data.Results.ToDictionary(
            item => item.Key,
            item => new StoredItem(item.Key, ToToken(item.Value), item.WriteLock));
    }

    public async Task<bool> TryWriteBatchAsync(IExecutionContext context, IReadOnlyList<ItemWrite> writes, CancellationToken ct)
    {
        if (writes.Count > MaxBatchSize)
        {
            throw new ArgumentException($"A protected batch holds at most {MaxBatchSize} keys, got {writes.Count}.", nameof(writes));
        }

        var body = new SetItemBatchBody(writes.Select(w => new SetItemBody(w.Key, w.Value, w.WriteLock!)).ToList());
        try
        {
            await _api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId, body, ct);
            return true;
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    private static JToken ToToken(object? value) => value switch
    {
        JToken token => token,
        null => JValue.CreateNull(),
        _ => JToken.FromObject(value),
    };
}
