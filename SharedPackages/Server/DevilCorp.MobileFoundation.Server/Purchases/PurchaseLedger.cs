using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace DevilCorp.MobileFoundation.Server.Purchases;

public enum PurchaseOutcome
{
    Delivered,
    AlreadyDelivered,
}

public sealed record PurchaseDelivery(PurchaseOutcome Outcome, WalletState Wallet);

/// <summary>What was delivered for one store transaction. Never evicted: it is the durable deduplication key.</summary>
public sealed class PurchaseRecord
{
    public const int CurrentSchemaVersion = 1;

    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonProperty("store")]
    public string Store { get; set; } = "";

    [JsonProperty("productId")]
    public string ProductId { get; set; } = "";

    [JsonProperty("grants")]
    public List<Grant> Grants { get; set; } = new();

    [JsonProperty("deliveredAtUnixMs")]
    public long DeliveredAtUnixMs { get; set; }
}

/// <summary>
/// One protected key per store transaction, written in the same atomic batch as the wallet credit. A retry
/// or a concurrent duplicate finds the key (or loses the writeLock race and finds it on re-read) and gets the
/// current wallet without a second credit.
/// </summary>
public static class PurchaseLedger
{
    /// <summary>Cloud Save allows 2000 keys per player; the margin leaves room for game keys.</summary>
    public const int MaxRecords = 1900;

    private const string KeyPrefix = "PURCHASE_";

    public static string KeyFor(string transactionId) =>
        KeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(transactionId))).ToLowerInvariant();

    /// <summary>
    /// Pure decision for one attempt; <paramref name="writes"/> is empty unless delivering now. The ledger is
    /// checked first: an already delivered transaction succeeds even if its product left the catalog since.
    /// A null <paramref name="product"/> (unknown to the catalog) is refused only for a new transaction.
    /// </summary>
    public static PurchaseOutcome Apply(
        StateSnapshot snapshot,
        WalletState wallet,
        PurchaseProduct? product,
        string store,
        string transactionId,
        WalletCatalog catalog,
        long nowMs,
        out IReadOnlyDictionary<string, object> writes)
    {
        var key = KeyFor(transactionId);
        if (snapshot.Contains(key))
        {
            writes = new Dictionary<string, object>();
            return PurchaseOutcome.AlreadyDelivered;
        }

        if (product == null)
        {
            throw new GameOperationException(ErrorCodes.PurchaseRejected, "unknown product");
        }

        if (wallet.PurchaseCount >= MaxRecords)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, "purchase ledger is full for this player");
        }

        WalletRules.Credit(wallet, product.Grants, catalog, nowMs / 1000);
        wallet.PurchaseCount++;
        writes = new Dictionary<string, object>
        {
            [WalletState.Key] = wallet,
            [key] = new PurchaseRecord
            {
                Store = store,
                ProductId = product.CatalogId,
                Grants = product.Grants,
                DeliveredAtUnixMs = nowMs,
            },
        };
        return PurchaseOutcome.Delivered;
    }

    /// <summary>Delivers a verified purchase once. <paramref name="transaction"/> must be guarded.</summary>
    public static Task<PurchaseDelivery> DeliverAsync(
        StateTransaction transaction,
        IExecutionContext context,
        IClock clock,
        PurchaseProduct? product,
        string store,
        string transactionId,
        WalletCatalog catalog,
        CancellationToken ct) =>
        transaction.RunAsync(context, new[] { KeyFor(transactionId) }, snapshot =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            var outcome = Apply(snapshot, wallet, product, store, transactionId, catalog, clock.UtcNowUnixMs, out var writes);
            return StateMutation.Write(new PurchaseDelivery(outcome, wallet), writes);
        }, ct);
}
