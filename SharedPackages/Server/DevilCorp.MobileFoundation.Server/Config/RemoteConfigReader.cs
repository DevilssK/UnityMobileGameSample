using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace DevilCorp.MobileFoundation.Server.Config;

/// <summary>
/// Game settings from Remote Config, cached 60 s per environment (Unity's in-memory cache guidance for Cloud
/// Code). Keys read here must not use player-targeted campaigns: the cache is shared by all players.
/// </summary>
public sealed class RemoteConfigReader
{
    public const long CacheDurationMs = 60_000;

    private readonly IGameApiClient _api;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<(string EnvironmentId, string Key), (JToken Value, long ExpiresAtMs)> _cache = new();

    public RemoteConfigReader(IGameApiClient api, IClock clock)
    {
        _api = api;
        _clock = clock;
    }

    public async Task<JToken> GetAsync(IExecutionContext context, string key, CancellationToken ct)
    {
        var cacheKey = (context.EnvironmentId, key);
        var now = _clock.UtcNowUnixMs;
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtMs > now)
        {
            return cached.Value;
        }

        var response = await _api.RemoteConfigSettings.AssignSettingsGetAsync(
            context, context.AccessToken, context.ProjectId, context.EnvironmentId, null!, new List<string> { key }, null!, ct);

        var settings = response?.Data?.Configs?.Settings;
        if (settings == null || !settings.TryGetValue(key, out var raw) || raw == null)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"Remote Config key {key} is missing");
        }

        var value = raw as JToken ?? JToken.FromObject(raw);
        _cache[cacheKey] = (value, now + CacheDurationMs);
        return value;
    }
}
