using System;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Purchases;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

public class StoreService
{
    private const string k_RejectedErrorCode = "PURCHASE_REJECTED";
    private const string k_RetryLaterErrorCode = "PURCHASE_RETRY_LATER";

    private readonly ILogger<StoreService> m_Logger;
    private IGameApiClient m_GameApiClient;
    private readonly PlayerEconomyService m_PlayerEconomyService;
    private readonly PurchaseRedeemer m_PurchaseRedeemer;

    private readonly int m_FreeCoinPackReward = 10;

    public StoreService(
        ILogger<StoreService> logger,
        IGameApiClient gameApiClient,
        PlayerEconomyService playerEconomyService,
        PurchaseRedeemer purchaseRedeemer)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerEconomyService = playerEconomyService;
        m_PurchaseRedeemer = purchaseRedeemer;
    }

    /// <summary>
    /// Redeems a real-money purchase. Rewards come only from the Economy real-money purchase
    /// configuration (.ecr), granted by Economy redeem; this function never credits anything itself.
    /// Returns the player's economy when the purchase is delivered now or was delivered earlier to
    /// this player, so the client can confirm the transaction with the store. Throws an error whose
    /// message starts with PURCHASE_REJECTED (do not retry) or PURCHASE_RETRY_LATER (keep pending).
    /// </summary>
    /// <param name="transactionId">Store transaction ID, used only for support correlation in logs.</param>
    [CloudCodeFunction("HandlePurchase")]
    public async Task<PlayerEconomyData> HandlePurchase(
        IExecutionContext context,
        string productId,
        string receipt,
        string transactionId)
    {
        m_Logger.LogInformation("Processing transaction {TransactionId} for product {ProductId} (correlation {CorrelationId})",
            transactionId, productId, context.CorrelationId);

        var result = await m_PurchaseRedeemer.RedeemAsync(context, productId, receipt);

        switch (result.Status)
        {
            case PurchaseRedemptionStatus.Delivered:
            case PurchaseRedemptionStatus.AlreadyDelivered:
            case PurchaseRedemptionStatus.Simulated:
                return await m_PlayerEconomyService.GetPlayerEconomyData(context)
                    ?? throw new InvalidOperationException("Failed to get player economy data");

            case PurchaseRedemptionStatus.RetryLater:
                throw new InvalidOperationException($"{k_RetryLaterErrorCode}: {result.Reason}");

            default:
                throw new InvalidOperationException($"{k_RejectedErrorCode}: {result.Reason}");
        }
    }

    [CloudCodeFunction("HandleFreeCoinPackPurchase")]
    public async Task<PlayerEconomyData> HandleFreeCoinPackPurchase(IExecutionContext context)
    {
        try
        {
            var economyData = await m_PlayerEconomyService.GetPlayerEconomyData(context)
                ?? throw new InvalidOperationException("Failed to get player economy data");

            if (economyData.HasPurchasedFreeCoinPack)
            {
                m_Logger.LogWarning("Free pack already purchased");
                return economyData;
            }

            // Lock first, credit second: concurrent requests cannot both credit. If the credit fails after
            // the lock, the player loses this low-value free pack rather than receiving it twice.
            if (!await m_PlayerEconomyService.TryClaimFreePackAsync(context))
            {
                return await m_PlayerEconomyService.GetPlayerEconomyData(context)
                    ?? throw new InvalidOperationException("Failed to get player economy data");
            }

            await m_PlayerEconomyService.UpdatePlayerCurrency(context, EconomyConstants.Currencies.k_Coin, m_FreeCoinPackReward);
            return await m_PlayerEconomyService.GetPlayerEconomyData(context)
                ?? throw new InvalidOperationException("Failed to get player economy data");
        }

        catch (Exception e)
        {
            m_Logger.LogError(e, "Error processing purchase for free coin pack");
            throw;
        }
    }
}
