using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace GemHunterUGSCloud.Models;

public class PlayerData
{
    [JsonProperty("hearts")]
    public int Hearts { get; set; }

    [JsonProperty("stars")]
    public int Stars { get; set; }

    [JsonProperty("gameAreasData")]
    public List<AreaData>? GameAreasData { get; set; }
    
    [JsonProperty("currentArea")]
    public AreaData? CurrentArea { get; set; }
    
    [JsonProperty("hasInfiniteHeartEffectActive")]
    public bool HasInfiniteHeartEffectActive { get; set; }

    public PlayerData()
    {
        GameAreasData = new List<AreaData>();
    }
}

/// <summary>
/// Consolidated response for player initialization that includes all necessary data
/// to set up the player's game state in a single call.
/// </summary>
public class PlayerInitializationResponse
{
    [JsonProperty("playerData")]
    public PlayerData PlayerData { get; set; } = new PlayerData();

    [JsonProperty("economyData")]
    public PlayerEconomyData EconomyData { get; set; } = new PlayerEconomyData();

    [JsonProperty("isNewPlayer")]
    public bool IsNewPlayer { get; set; }

    [JsonProperty("initializationTimestamp")]
    public long InitializationTimestamp { get; set; }
}
