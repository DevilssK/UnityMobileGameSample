using Xunit;

namespace DevilCorp.MobileFoundation.Purchases.Tests
{
    public class TransactionLogIdTests
    {
        // With Unity IAP 5 the Google Play transaction id is the purchase token, so it must never reach a log.
        private const string GooglePurchaseToken = "opaque-token.AO-J1OzExampleExampleExample";

        [Fact]
        public void From_SameTransaction_ReturnsSameShortHexId()
        {
            var first = TransactionLogId.From(GooglePurchaseToken);

            Assert.Equal(first, TransactionLogId.From(GooglePurchaseToken));
            Assert.Matches("^[0-9a-f]{12}$", first);
        }

        [Fact]
        public void From_KnownValue_MatchesBackendAlgorithm()
        {
            // First 12 hex chars of SHA-256("abc"); the Cloud Code module must produce the same value.
            Assert.Equal("ba7816bf8f01", TransactionLogId.From("abc"));
        }

        [Fact]
        public void ToString_DoesNotContainTransactionIdOrReceipt()
        {
            var purchase = new StorePurchase("coin_pack", GooglePurchaseToken, "{\"Payload\":\"secret\"}");

            var text = purchase.ToString();

            Assert.DoesNotContain(GooglePurchaseToken, text);
            Assert.DoesNotContain("secret", text);
            Assert.Contains("coin_pack", text);
            Assert.Contains(TransactionLogId.From(GooglePurchaseToken), text);
        }
    }
}
