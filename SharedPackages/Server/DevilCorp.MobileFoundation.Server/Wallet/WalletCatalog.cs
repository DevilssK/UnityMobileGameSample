using System;
using System.Collections.Generic;
using System.Linq;
using DevilCorp.MobileFoundation.Server.State;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevilCorp.MobileFoundation.Server.Wallet;

public enum ResourceKind
{
    Unknown,
    Currency,
    Item,
    TimedEffect,
}

/// <summary>Which resources a wallet holds and how they start; read from game configuration.</summary>
public sealed class WalletCatalog
{
    [JsonProperty("currencies")]
    public Dictionary<string, ResourceDefinition> Currencies { get; set; } = new();

    [JsonProperty("items")]
    public Dictionary<string, ResourceDefinition> Items { get; set; } = new();

    [JsonProperty("timedEffects")]
    public Dictionary<string, TimedEffectDefinition> TimedEffects { get; set; } = new();

    public ResourceKind KindOf(string id) =>
        Currencies.ContainsKey(id) ? ResourceKind.Currency
        : Items.ContainsKey(id) ? ResourceKind.Item
        : TimedEffects.ContainsKey(id) ? ResourceKind.TimedEffect
        : ResourceKind.Unknown;

    /// <summary>Parses and validates; any invalid value is a configuration error, never a silent default.</summary>
    public static WalletCatalog Parse(JToken json)
    {
        WalletCatalog? catalog;
        try
        {
            catalog = json.ToObject<WalletCatalog>();
        }
        catch (JsonException e)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"invalid wallet catalog: {e.Message}");
        }

        if (catalog == null
            || catalog.Currencies.Values.Concat(catalog.Items.Values).Any(r => r == null || r.Initial < 0)
            || catalog.TimedEffects.Values.Any(e => e == null || e.DurationSeconds <= 0))
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, "invalid wallet catalog values");
        }

        var ids = catalog.Currencies.Keys.Concat(catalog.Items.Keys).Concat(catalog.TimedEffects.Keys).ToList();
        if (ids.Count != ids.Distinct(StringComparer.Ordinal).Count())
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, "a wallet resource id is declared twice");
        }

        return catalog;
    }
}

public sealed class ResourceDefinition
{
    [JsonProperty("initial")]
    public long Initial { get; set; }
}

public sealed class TimedEffectDefinition
{
    [JsonProperty("durationSeconds")]
    public long DurationSeconds { get; set; }
}
