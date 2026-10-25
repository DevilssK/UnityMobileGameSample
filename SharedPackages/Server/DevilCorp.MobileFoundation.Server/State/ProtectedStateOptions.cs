using System;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>
/// Guard applied to every guarded transaction. Cloud Save has no create-only write (development spike,
/// 2026-10-25), so an initialization based on a stale "absent" read could overwrite newer state. A Cloud Code
/// C# function is killed after 15 s, so such a stale write lands within 15 s of its read: refusing every
/// mutation for <see cref="GracePeriod"/> after creation means it can only overwrite another initialization.
/// </summary>
public sealed class ProtectedStateOptions
{
    public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromSeconds(30);

    public TimeSpan GracePeriod { get; init; } = DefaultGracePeriod;
}
