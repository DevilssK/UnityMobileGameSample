using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// Unity IAP receipt envelope (<c>{"Store","TransactionID","Payload"}</c>) reduced to the fields
/// Economy redeem expects for each store. Never log the values of this type: they are store proofs.
/// </summary>
public sealed class StoreReceipt
{
    private const string GooglePlayStoreName = "GooglePlay";
    private const string AppleAppStoreName = "AppleAppStore";
    private const string FakeStoreName = "fake";

    private StoreReceipt(StoreKind store, string transactionId)
    {
        Store = store;
        TransactionId = transactionId;
    }

    public StoreKind Store { get; }

    /// <summary>Store transaction identifier; safe to log for support correlation.</summary>
    public string TransactionId { get; }

    /// <summary>Google Play purchase data JSON (the <c>json</c> field of the payload).</summary>
    public string? GooglePurchaseData { get; private init; }

    /// <summary>Signature of <see cref="GooglePurchaseData"/> (the <c>signature</c> field of the payload).</summary>
    public string? GoogleSignature { get; private init; }

    /// <summary>Apple App Store receipt (the payload itself).</summary>
    public string? AppleReceipt { get; private init; }

    /// <summary>
    /// Parses a Unity IAP receipt. Returns false when the JSON is invalid, the store is unknown,
    /// or a field required by the store is missing.
    /// </summary>
    public static bool TryParse(string? receiptJson, out StoreReceipt? receipt)
    {
        receipt = null;
        var envelope = TryParseObject(receiptJson);
        if (envelope == null)
        {
            return false;
        }

        var store = ParseStore(envelope.Value<string>("Store"));
        var transactionId = envelope.Value<string>("TransactionID") ?? string.Empty;
        var payload = envelope.Value<string>("Payload");

        switch (store)
        {
            case StoreKind.GooglePlay:
                var googlePayload = TryParseObject(payload);
                var purchaseData = googlePayload?.Value<string>("json");
                var signature = googlePayload?.Value<string>("signature");
                if (string.IsNullOrEmpty(purchaseData) || string.IsNullOrEmpty(signature))
                {
                    return false;
                }

                receipt = new StoreReceipt(store, transactionId)
                {
                    GooglePurchaseData = purchaseData,
                    GoogleSignature = signature
                };
                return true;

            case StoreKind.AppleAppStore:
                if (string.IsNullOrEmpty(payload))
                {
                    return false;
                }

                receipt = new StoreReceipt(store, transactionId) { AppleReceipt = payload };
                return true;

            case StoreKind.Fake:
                receipt = new StoreReceipt(store, transactionId);
                return true;

            default:
                return false;
        }
    }

    private static StoreKind ParseStore(string? storeName)
    {
        if (string.Equals(storeName, GooglePlayStoreName, StringComparison.OrdinalIgnoreCase))
        {
            return StoreKind.GooglePlay;
        }

        if (string.Equals(storeName, AppleAppStoreName, StringComparison.OrdinalIgnoreCase))
        {
            return StoreKind.AppleAppStore;
        }

        if (string.Equals(storeName, FakeStoreName, StringComparison.OrdinalIgnoreCase))
        {
            return StoreKind.Fake;
        }

        return StoreKind.Unknown;
    }

    private static JObject? TryParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JToken.Parse(json) as JObject;
        }
        catch (JsonReaderException)
        {
            return null;
        }
    }
}
