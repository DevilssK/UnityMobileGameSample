using DevilCorp.MobileFoundation.Purchases;
using Xunit;

namespace DevilCorp.MobileFoundation.Purchases.Tests
{
    public class PackageSmokeTests
    {
        [Fact]
        public void PurchaseOutcome_HasDeliveredValue()
        {
            Assert.Equal("Delivered", PurchaseOutcome.Delivered.ToString());
        }
    }
}
