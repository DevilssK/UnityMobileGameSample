using System;

namespace DevilCorp.MobileFoundation.Server.State;

public interface IClock
{
    long UtcNowUnixMs { get; }
}

public sealed class SystemClock : IClock
{
    public long UtcNowUnixMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
