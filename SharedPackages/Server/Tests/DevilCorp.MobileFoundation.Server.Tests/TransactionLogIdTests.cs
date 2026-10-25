using DevilCorp.MobileFoundation.Server.Purchases;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class TransactionLogIdTests
{
    [Fact]
    public void From_KnownValue_MatchesClientPackageAlgorithm()
    {
        // Same expected value as the client package test: logs of one transaction correlate on both sides.
        Assert.Equal("ba7816bf8f01", TransactionLogId.From("abc"));
    }

    [Fact]
    public void From_GooglePurchaseToken_DoesNotExposeToken()
    {
        const string purchaseToken = "opaque-token.AO-J1OzExampleExampleExample";

        var logId = TransactionLogId.From(purchaseToken);

        Assert.Matches("^[0-9a-f]{12}$", logId);
        Assert.DoesNotContain(logId, purchaseToken);
    }

    [Fact]
    public void From_Missing_ReturnsPlaceholder()
    {
        Assert.Equal("none", TransactionLogId.From(null));
        Assert.Equal("none", TransactionLogId.From(""));
    }
}
