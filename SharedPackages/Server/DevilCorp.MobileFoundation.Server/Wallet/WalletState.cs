using System.Collections.Generic;
using Newtonsoft.Json;

namespace DevilCorp.MobileFoundation.Server.Wallet;

/// <summary>
/// The protected wallet of one player, stored under <see cref="Key"/>. It also guards the whole protected
/// state: no mutation is accepted before it exists and its creation grace period has passed.
/// </summary>
public sealed class WalletState
{
    public const string Key = "WALLET";
    public const int CurrentSchemaVersion = 1;
    public const int MaxRecentOperations = 50;

    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonProperty("createdAtUnixMs")]
    public long CreatedAtUnixMs { get; set; }

    [JsonProperty("currencies")]
    public Dictionary<string, long> Currencies { get; set; } = new();

    [JsonProperty("items")]
    public Dictionary<string, int> Items { get; set; } = new();

    /// <summary>Effect id → expiry in Unix seconds.</summary>
    [JsonProperty("timedEffects")]
    public Dictionary<string, long> TimedEffects { get; set; } = new();

    /// <summary>One-time claims already granted (for example a free pack).</summary>
    [JsonProperty("claims")]
    public HashSet<string> Claims { get; set; } = new();

    /// <summary>Most recent client operation ids, oldest first, for retry deduplication.</summary>
    [JsonProperty("recentOperations")]
    public List<string> RecentOperations { get; set; } = new();

    [JsonProperty("purchaseCount")]
    public int PurchaseCount { get; set; }
}
