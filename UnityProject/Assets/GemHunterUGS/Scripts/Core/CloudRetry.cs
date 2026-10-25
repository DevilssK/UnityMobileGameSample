using System;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Core;

namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Retries idempotent Cloud Code calls refused with RETRY_LATER, for example during the 30 s that follow a
    /// new player's creation. 8 attempts 5 s apart cover that window. The caller passes the same operation id
    /// on every attempt, so the server applies the action once.
    /// </summary>
    public static class CloudRetry
    {
        private const int k_MaxAttempts = 8;
        private static readonly TimeSpan s_Delay = TimeSpan.FromSeconds(5);

        public static Task<T> RunAsync<T>(Func<Task<T>> call) =>
            TransientRetry.RunAsync(call, IsRetryLater, k_MaxAttempts, s_Delay);

        // PURCHASE_RETRY_LATER is handled by the purchase coordinator, never here.
        private static bool IsRetryLater(Exception e) =>
            e.Message.Contains("RETRY_LATER") && !e.Message.Contains("PURCHASE_RETRY_LATER");
    }
}
