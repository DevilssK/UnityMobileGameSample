using System;
using System.Security.Cryptography;
using System.Text;

namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// Non-reversible identifier for logs. A store transaction id is not safe to log: with Unity IAP 5 the
/// Google Play transaction id is the purchase token. Same algorithm as the client package
/// (DevilCorp.MobileFoundation.Purchases.TransactionLogId), so both logs of one transaction match.
/// </summary>
internal static class TransactionLogId
{
    private const int Length = 12;
    private const string Missing = "none";

    public static string From(string? transactionId)
    {
        if (string.IsNullOrEmpty(transactionId))
        {
            return Missing;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(transactionId));
        return Convert.ToHexString(hash, 0, Length / 2).ToLowerInvariant();
    }
}
