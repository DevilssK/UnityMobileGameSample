using System;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Purchases;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Purchases;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services;

/// <summary>
/// Real-money purchases and the free coin pack. The server is the only one to grant: each store transaction
/// is delivered once, in the same atomic write as its durable ledger key.
/// </summary>
public class StoreService
{
    private const string k_FakeStoreName = "fake";

    private readonly ILogger<StoreService> m_Logger;
    private readonly GemHunterState m_State;
    private readonly PurchaseCatalog m_Catalog;

    public StoreService(ILogger<StoreService> logger, GemHunterState state, PurchaseCatalog catalog)
    {
        m_Logger = logger;
        m_State = state;
        m_Catalog = catalog;
    }

    /// <summary>
    /// Returns the player's economy when the purchase is delivered now or was delivered earlier to this player,
    /// so the client can confirm it with the store. Errors start with PURCHASE_REJECTED (never retry) or
    /// PURCHASE_RETRY_LATER (keep the purchase pending).
    /// </summary>
    /// <param name="transactionId">Store transaction ID (the purchase token on Google Play): logged only as <see cref="TransactionLogId"/>.</param>
    [CloudCodeFunction("HandlePurchase")]
    public async Task<PlayerEconomyData> HandlePurchase(IExecutionContext context, string productId, string receipt, string transactionId)
    {
        if (!StoreReceipt.TryParse(receipt, out var parsed) || parsed == null || string.IsNullOrEmpty(parsed.TransactionId))
        {
            throw Rejected(context, productId, transactionId, "unreadable receipt");
        }

        var logId = TransactionLogId.From(parsed.TransactionId);
        if (parsed.Store != StoreKind.Fake)
        {
            // Apple/Google verification is the next step of the plan; until then nothing is granted.
            m_Logger.LogWarning("Purchase {TransactionLogId} of {ProductId} from {Store} kept pending: store verification not available (correlation {CorrelationId})",
                logId, productId, parsed.Store, context.CorrelationId);
            throw new InvalidOperationException($"{ErrorCodes.PurchaseRetryLater}: store verification not available yet");
        }

        if (!FakeStorePolicy.IsAllowed(context.EnvironmentName))
        {
            throw Rejected(context, productId, parsed.TransactionId, "fake store outside development");
        }

        // Development only: the fake store has no verified product id, so the catalog id from the client is used.
        var product = m_Catalog.FindByCatalogId(productId) ?? throw Rejected(context, productId, parsed.TransactionId, "unknown product");

        PurchaseDelivery delivery;
        try
        {
            var tuning = await m_State.TuningAsync(context, CancellationToken.None);
            await m_State.EnsureInitializedAsync(context, CancellationToken.None);
            delivery = await PurchaseLedger.DeliverAsync(m_State.Transaction, context, m_State.Clock, product, k_FakeStoreName,
                parsed.TransactionId, tuning.Wallet, CancellationToken.None);
            m_Logger.LogInformation("Purchase {TransactionLogId} of {ProductId} from {Store}: {Outcome} (correlation {CorrelationId})",
                logId, productId, k_FakeStoreName, delivery.Outcome, context.CorrelationId);
            return EconomyView.From(delivery.Wallet, tuning.Wallet);
        }
        catch (GameOperationException e)
        {
            // A paid purchase is never refused for a transient or configuration problem: keep it pending.
            m_Logger.LogWarning("Purchase {TransactionLogId} of {ProductId} kept pending: {Reason} (correlation {CorrelationId})",
                logId, productId, e.Message, context.CorrelationId);
            throw new InvalidOperationException($"{ErrorCodes.PurchaseRetryLater}: {e.Message}");
        }
    }

    [CloudCodeFunction("HandleFreeCoinPackPurchase")]
    public async Task<PlayerEconomyData> HandleFreeCoinPackPurchase(IExecutionContext context)
    {
        var economy = await m_State.RunAsync(context, Array.Empty<string>(), (snapshot, tuning) =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            return FreeCoinPackRules.Claim(wallet, tuning, m_State.NowUnixSec)
                ? StateMutation.Write(EconomyView.From(wallet, tuning.Wallet), (WalletState.Key, (object)wallet))
                : StateMutation.Read(EconomyView.From(wallet, tuning.Wallet));
        }, CancellationToken.None);

        m_Logger.LogInformation("Free coin pack handled (correlation {CorrelationId})", context.CorrelationId);
        return economy;
    }

    private InvalidOperationException Rejected(IExecutionContext context, string productId, string? transactionId, string reason)
    {
        m_Logger.LogWarning("Purchase {TransactionLogId} of {ProductId} rejected: {Reason} (correlation {CorrelationId})",
            TransactionLogId.From(transactionId), productId, reason, context.CorrelationId);
        return new InvalidOperationException($"{ErrorCodes.PurchaseRejected}: {reason}");
    }
}
