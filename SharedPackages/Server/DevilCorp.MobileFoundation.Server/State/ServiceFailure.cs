using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Shared;

namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>
/// Maps an unavailable UGS service to <see cref="ErrorCodes.RetryLater"/>, the code clients retry on. Only the
/// status code or exception type is kept: response bodies may echo request data.
/// </summary>
internal static class ServiceFailure
{
    public static bool IsTransient(Exception e, CancellationToken ct) =>
        e is ApiException or HttpRequestException or TimeoutException
        || (e is TaskCanceledException && !ct.IsCancellationRequested);

    public static GameOperationException RetryLater(string service, Exception e)
    {
        var reason = e is ApiException { Response: not null } api ? $"HTTP {(int)api.Response.StatusCode}" : e.GetType().Name;
        return new GameOperationException(ErrorCodes.RetryLater, $"{service} unavailable ({reason})");
    }
}
