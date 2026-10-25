using Newtonsoft.Json;

namespace DevilCorp.MobileFoundation.Server.State;

internal static class StateJson
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);
}
