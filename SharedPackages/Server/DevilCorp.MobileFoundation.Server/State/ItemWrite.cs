using Newtonsoft.Json.Linq;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>
/// One key of a protected batch write. <see cref="WriteLock"/> is the lock read with the item; null means the
/// key did not exist. Cloud Save then writes it unconditionally, so only deterministic new keys or the
/// guarded initialization may use null.
/// </summary>
public sealed record ItemWrite(string Key, JToken Value, string? WriteLock);
