using System;
using GemHunterUGSCloud.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GemHunterUGSCloud.Tests.Services;

public class InfiniteHeartStateTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveInfiniteHeartState_WhenInstanceDataPresent_UsesIt()
    {
        var instanceData = JObject.Parse("{\"active\":false,\"activatedAt\":100}");

        var state = PlayerEconomyService.ResolveInfiniteHeartState(instanceData, CreatedAt);

        Assert.Equal((false, 100L), state);
    }

    [Fact]
    public void ResolveInfiniteHeartState_WhenInstanceDataMissing_UsesCreatedDate()
    {
        var state = PlayerEconomyService.ResolveInfiniteHeartState(null, CreatedAt);

        Assert.Equal((true, 1792800000L), state);
    }

    [Fact]
    public void ResolveInfiniteHeartState_WhenInstanceDataUnreadable_UsesCreatedDate()
    {
        var state = PlayerEconomyService.ResolveInfiniteHeartState(JObject.Parse("{}"), CreatedAt);

        Assert.Equal((true, 1792800000L), state);
    }

    [Fact]
    public void ResolveInfiniteHeartState_WhenNothingKnown_ReturnsNull()
    {
        Assert.Null(PlayerEconomyService.ResolveInfiniteHeartState(null, null));
    }
}
