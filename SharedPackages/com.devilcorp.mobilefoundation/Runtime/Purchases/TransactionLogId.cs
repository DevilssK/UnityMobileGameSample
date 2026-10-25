#nullable enable

using System.Security.Cryptography;
using System.Text;

namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Non-reversible identifier for logs. A store transaction id is not safe to log: with Unity IAP 5 the
    /// Google Play transaction id is the purchase token. The backend uses the same algorithm, so client and
    /// server logs of one transaction share this value.
    /// </summary>
    public static class TransactionLogId
    {
        private const int Length = 12;

        /// <summary>First 12 lowercase hex characters of SHA-256 over the UTF-8 transaction id.</summary>
        public static string From(string transactionId)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(transactionId));
                var builder = new StringBuilder(Length);
                for (var i = 0; i < Length / 2; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
