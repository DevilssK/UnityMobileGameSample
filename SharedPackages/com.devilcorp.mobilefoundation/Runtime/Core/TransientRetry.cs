#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Core
{
    /// <summary>
    /// Bounded retry of a backend call refused for a transient reason (for example RETRY_LATER during the
    /// protected wallet's creation grace period). Only retry calls that are idempotent on the server, such as
    /// calls carrying the same operation id: a retry must never apply an action twice.
    /// </summary>
    public static class TransientRetry
    {
        public static async Task<T> RunAsync<T>(
            Func<Task<T>> call,
            Func<Exception, bool> isTransient,
            int maxAttempts,
            TimeSpan delay,
            Func<TimeSpan, Task>? wait = null,
            CancellationToken cancellationToken = default)
        {
            wait ??= d => Task.Delay(d, cancellationToken);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await call();
                }
                catch (Exception e) when (attempt < maxAttempts && isTransient(e))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await wait(delay);
                }
            }
        }
    }
}
