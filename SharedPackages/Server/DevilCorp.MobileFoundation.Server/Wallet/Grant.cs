using Newtonsoft.Json;

namespace DevilCorp.MobileFoundation.Server.Wallet;

/// <summary>An amount of one wallet resource (currency, item or timed effect units).</summary>
public sealed record Grant(
    [property: JsonProperty("id")] string Id,
    [property: JsonProperty("amount")] long Amount);
