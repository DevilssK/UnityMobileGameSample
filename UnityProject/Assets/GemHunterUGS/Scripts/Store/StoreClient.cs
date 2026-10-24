using System;
using System.Threading;
using DevilCorp.MobileFoundation.Purchases;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Bridges the store UI to the foundation purchase service. It never talks to the store SDK and never
    /// grants anything: purchases go through <see cref="IPurchaseService"/>, which validates them server-side
    /// before confirming them with the store.
    /// </summary>
    public class StoreClient : MonoBehaviour
    {
        [SerializeField]
        private StoreUIController m_StoreUIController;

        // Product IDs must match the Economy real-money purchase IDs
        [SerializeField]
        private string m_BundlePackProductID = GemHunterProductCatalog.k_BundlePack;
        [SerializeField]
        private string m_MegaPackProductID = GemHunterProductCatalog.k_MegaPack;

        private IPurchaseService m_PurchaseService;
        private PlayerEconomyManagerClient m_PlayerEconomyClient;
        private CloudBindingsProvider m_BindingsProvider;

        // Cancels only the UI wait when the store closes; paid purchases keep being validated.
        private CancellationTokenSource m_WaitCancellation = new();

        /// <summary>Localized-free product ID of a purchase delivered to the player.</summary>
        public event Action<string> SuccessfullyPurchased;

        /// <summary>A purchase ended without delivery (cancelled, failed, refused or pending): the UI can unlock.</summary>
        public event Action PurchaseNotDelivered;

        private void Start()
        {
            if (m_StoreUIController == null)
            {
                m_StoreUIController = GetComponent<StoreUIController>();
            }

            m_PurchaseService = GameSystemLocator.Get<IPurchaseService>();
            m_PlayerEconomyClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();

            m_StoreUIController.ClickPurchaseBundlePack += HandleBundlePackPurchase;
            m_StoreUIController.ClickPurchaseMegaPack += HandleMegaPackPurchase;
            m_StoreUIController.ClickPurchaseCoinPack += HandleCoinPackPurchase;
            m_StoreUIController.ClickPurchaseFreeCoinPack += HandleCoinPackFreePurchase;

            // Opening the store is an explicit occasion to re-validate purchases left pending.
            RetryPendingPurchases();
        }

        private void OnDestroy()
        {
            m_WaitCancellation.Cancel();
            m_WaitCancellation.Dispose();

            if (m_StoreUIController == null)
            {
                return;
            }

            m_StoreUIController.ClickPurchaseBundlePack -= HandleBundlePackPurchase;
            m_StoreUIController.ClickPurchaseMegaPack -= HandleMegaPackPurchase;
            m_StoreUIController.ClickPurchaseCoinPack -= HandleCoinPackPurchase;
            m_StoreUIController.ClickPurchaseFreeCoinPack -= HandleCoinPackFreePurchase;
        }

        private void HandleBundlePackPurchase() => Purchase(m_BundlePackProductID);

        private void HandleMegaPackPurchase() => Purchase(m_MegaPackProductID);

        private void HandleCoinPackPurchase(int coinAmount)
        {
            if (GemHunterProductCatalog.CoinPacks.TryGetValue(coinAmount, out var productId))
            {
                Purchase(productId);
            }
            else
            {
                Logger.LogError($"No product ID found for coin amount: {coinAmount}");
                PurchaseNotDelivered?.Invoke();
            }
        }

        private async void Purchase(string productId)
        {
            try
            {
                var result = await m_PurchaseService.PurchaseAsync(productId, m_WaitCancellation.Token);
                if (result.Outcome == PurchaseOutcome.Delivered)
                {
                    SuccessfullyPurchased?.Invoke(productId);
                    return;
                }

                Logger.LogWarning($"Purchase not delivered: {result}");
                PurchaseNotDelivered?.Invoke();
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                PurchaseNotDelivered?.Invoke();
            }
        }

        private async void HandleCoinPackFreePurchase()
        {
            try
            {
                var economyData = await m_BindingsProvider.GemHunterBindings.HandleFreeCoinPackPurchase();
                m_PlayerEconomyClient.HandleEconomyUpdate(economyData);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Free coin pack not claimed: {e.GetType().Name}");
                PurchaseNotDelivered?.Invoke();
            }
        }

        private async void RetryPendingPurchases()
        {
            try
            {
                await m_PurchaseService.RetryPendingFulfillmentsAsync(m_WaitCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Store closed: remaining retries happen on the next occasion.
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }
    }
}
