using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevilCorp.MobileFoundation.Core.Tests
{
    public class TransientRetryTests
    {
        private readonly List<TimeSpan> _waits = new List<TimeSpan>();

        private Task Wait(TimeSpan delay)
        {
            _waits.Add(delay);
            return Task.CompletedTask;
        }

        private static bool IsRetryLater(Exception e) => e.Message.StartsWith("RETRY_LATER");

        [Fact]
        public async Task RunAsync_TransientThenSuccess_RetriesAfterDelay()
        {
            var calls = 0;

            var result = await TransientRetry.RunAsync(() =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new InvalidOperationException("RETRY_LATER: state was just created");
                }

                return Task.FromResult(42);
            }, IsRetryLater, 5, TimeSpan.FromSeconds(5), Wait, CancellationToken.None);

            Assert.Equal(42, result);
            Assert.Equal(3, calls);
            Assert.Equal(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5) }, _waits);
        }

        [Fact]
        public async Task RunAsync_AlwaysTransient_ThrowsLastErrorAfterMaxAttempts()
        {
            var calls = 0;

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => TransientRetry.RunAsync<int>(() =>
            {
                calls++;
                throw new InvalidOperationException("RETRY_LATER: " + calls);
            }, IsRetryLater, 4, TimeSpan.FromSeconds(1), Wait, CancellationToken.None));

            Assert.Equal(4, calls);
            Assert.Equal("RETRY_LATER: 4", error.Message);
            Assert.Equal(3, _waits.Count);
        }

        [Fact]
        public async Task RunAsync_NonTransient_ThrowsImmediately()
        {
            var calls = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() => TransientRetry.RunAsync<int>(() =>
            {
                calls++;
                throw new InvalidOperationException("INSUFFICIENT_FUNDS: need 20 COIN");
            }, IsRetryLater, 5, TimeSpan.FromSeconds(1), Wait, CancellationToken.None));

            Assert.Equal(1, calls);
            Assert.Empty(_waits);
        }

        [Fact]
        public async Task RunAsync_CancelledWhileWaiting_StopsRetrying()
        {
            using var cancellation = new CancellationTokenSource();
            var calls = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TransientRetry.RunAsync<int>(() =>
            {
                calls++;
                cancellation.Cancel();
                throw new InvalidOperationException("RETRY_LATER");
            }, IsRetryLater, 5, TimeSpan.FromSeconds(1), Wait, cancellation.Token));

            Assert.Equal(1, calls);
        }
    }
}
