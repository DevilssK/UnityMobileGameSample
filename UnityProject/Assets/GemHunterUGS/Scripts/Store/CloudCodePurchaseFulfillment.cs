using System;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Purchases;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Services.CloudCode;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Validates a paid transaction with the Gem Hunter Cloud Code module (HandlePurchase), which redeems it
    /// through Economy. The client never grants anything: it only displays the economy returned by the server.
    /// </summary>
    public class CloudCodePurchaseFulfillment : IPurchaseFulfillment
    {
        // Prefix of the error raised by StoreService.HandlePurchase for a definitive refusal.
        private const string k_RejectedErrorCode = "PURCHASE_REJECTED";

        private readonly CloudBindingsProvider m_BindingsProvider;
        private readonly PlayerEconomyManagerClient m_EconomyClient;
        private readonly PlayerAuthenticationManager m_AuthenticationManager;

        public CloudCodePurchaseFulfillment(
            CloudBindingsProvider bindingsProvider,
            PlayerEconomyManagerClient economyClient,
            PlayerAuthenticationManager authenticationManager)
        {
            m_BindingsProvider = bindingsProvider;
            m_EconomyClient = economyClient;
            m_AuthenticationManager = authenticationManager;
        }

        public async Task<FulfillmentStatus> FulfillAsync(StorePurchase purchase, CancellationToken cancellationToken)
        {
            if (!m_AuthenticationManager.IsSignedIn)
            {
                // Paid but nobody to credit yet: keep it pending until the next sign-in.
                return FulfillmentStatus.RetryLater;
            }

            try
            {
                var economyData = await m_BindingsProvider.GemHunterBindings.HandlePurchase(
                    purchase.ProductId, purchase.Receipt, purchase.TransactionId);
                m_EconomyClient.HandleEconomyUpdate(economyData);
                return FulfillmentStatus.Delivered;
            }
            catch (CloudCodeException e) when (e.Message.Contains(k_RejectedErrorCode))
            {
                Logger.LogWarning($"Purchase {purchase} rejected by the server");
                return FulfillmentStatus.Rejected;
            }
            catch (Exception e)
            {
                // Network, service or unknown error: the transaction stays pending and is retried later.
                Logger.LogWarning($"Purchase {purchase} not validated yet: {e.GetType().Name}");
                return FulfillmentStatus.RetryLater;
            }
        }
    }
}
