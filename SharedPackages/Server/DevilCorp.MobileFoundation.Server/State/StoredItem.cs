using Newtonsoft.Json.Linq;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>A protected Cloud Save item as read, with the writeLock to send back on update.</summary>
public sealed record StoredItem(string Key, JToken Value, string WriteLock);
