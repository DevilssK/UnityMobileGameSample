using GemHunterUGSCloud.Purchases;
using Xunit;

namespace GemHunterUGSCloud.Tests.Purchases;

public class FakeStorePolicyTests
{
    [Theory]
    [InlineData("development")]
    [InlineData("Development")]
    public void IsAllowed_WhenDevelopment_ReturnsTrue(string environmentName)
    {
        Assert.True(FakeStorePolicy.IsAllowed(environmentName));
    }

    [Theory]
    [InlineData("production")]
    [InlineData("staging")]
    [InlineData("dev")]
    [InlineData("development2")]
    [InlineData(" development")]
    public void IsAllowed_WhenProductionOrOther_ReturnsFalse(string environmentName)
    {
        Assert.False(FakeStorePolicy.IsAllowed(environmentName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsAllowed_WhenNullOrEmpty_ReturnsFalse(string? environmentName)
    {
        Assert.False(FakeStorePolicy.IsAllowed(environmentName));
    }
}
