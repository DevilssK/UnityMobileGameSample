using GemHunterUGSCloud.Purchases;
using Xunit;

namespace GemHunterUGSCloud.Tests.Purchases;

public class StoreReceiptTests
{
    private const string GooglePayload = "{\"json\":\"{\\\"orderId\\\":\\\"GPA.1\\\"}\",\"signature\":\"sig==\"}";

    private static string Envelope(string store, string? payload, string transactionId = "GPA.1")
    {
        var payloadJson = payload == null ? "null" : Newtonsoft.Json.JsonConvert.ToString(payload);
        return $"{{\"Store\":\"{store}\",\"TransactionID\":\"{transactionId}\",\"Payload\":{payloadJson}}}";
    }

    [Fact]
    public void TryParse_WhenGooglePlayReceipt_ExtractsPurchaseDataAndSignature()
    {
        var parsed = StoreReceipt.TryParse(Envelope("GooglePlay", GooglePayload), out var receipt);

        Assert.True(parsed);
        Assert.Equal(StoreKind.GooglePlay, receipt!.Store);
        Assert.Equal("{\"orderId\":\"GPA.1\"}", receipt.GooglePurchaseData);
        Assert.Equal("sig==", receipt.GoogleSignature);
        Assert.Equal("GPA.1", receipt.TransactionId);
    }

    [Fact]
    public void TryParse_WhenAppleAppStoreReceipt_UsesPayloadAsReceipt()
    {
        var parsed = StoreReceipt.TryParse(Envelope("AppleAppStore", "MIIT..."), out var receipt);

        Assert.True(parsed);
        Assert.Equal(StoreKind.AppleAppStore, receipt!.Store);
        Assert.Equal("MIIT...", receipt.AppleReceipt);
    }

    [Fact]
    public void TryParse_WhenStoreNameHasDifferentCase_IsRecognised()
    {
        var parsed = StoreReceipt.TryParse(Envelope("googleplay", GooglePayload), out var receipt);

        Assert.True(parsed);
        Assert.Equal(StoreKind.GooglePlay, receipt!.Store);
    }

    [Fact]
    public void TryParse_WhenFakeStore_ReturnsFake()
    {
        var parsed = StoreReceipt.TryParse(Envelope("fake", "ThisIsFakeReceiptData"), out var receipt);

        Assert.True(parsed);
        Assert.Equal(StoreKind.Fake, receipt!.Store);
    }

    [Fact]
    public void TryParse_WhenStoreIsLegacyApple_IsRejected()
    {
        Assert.False(StoreReceipt.TryParse(Envelope("apple", "MIIT..."), out _));
    }

    [Fact]
    public void TryParse_WhenGooglePayloadIsNotJson_IsRejected()
    {
        Assert.False(StoreReceipt.TryParse(Envelope("GooglePlay", "not json"), out _));
    }

    [Fact]
    public void TryParse_WhenGoogleSignatureMissing_IsRejected()
    {
        var payload = "{\"json\":\"{\\\"orderId\\\":\\\"GPA.1\\\"}\"}";

        Assert.False(StoreReceipt.TryParse(Envelope("GooglePlay", payload), out _));
    }

    [Fact]
    public void TryParse_WhenApplePayloadMissing_IsRejected()
    {
        Assert.False(StoreReceipt.TryParse(Envelope("AppleAppStore", null), out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void TryParse_WhenReceiptIsNullEmptyOrInvalidJson_IsRejected(string? receiptJson)
    {
        Assert.False(StoreReceipt.TryParse(receiptJson, out var receipt));
        Assert.Null(receipt);
    }
}
