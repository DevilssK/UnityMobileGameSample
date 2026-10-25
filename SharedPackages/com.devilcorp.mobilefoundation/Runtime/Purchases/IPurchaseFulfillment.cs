using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Game-specific backend validation. The backend is the single owner of the grant: it must return
    /// <see cref="FulfillmentStatus.Delivered"/> again, without granting twice, for a transaction it already delivered.
    /// </summary>
    public interface IPurchaseFulfillment
    {
        Task<FulfillmentStatus> FulfillAsync(StorePurchase purchase, CancellationToken cancellationToken);
    }
}
