using System;

namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// The Unity IAP fake store is accepted only in this explicit environment, and never grants anything.
/// Any other environment, including a missing name, rejects it.
/// </summary>
internal static class FakeStorePolicy
{
    public const string AllowedEnvironmentName = "development";

    public static bool IsAllowed(string? environmentName) =>
        string.Equals(environmentName, AllowedEnvironmentName, StringComparison.OrdinalIgnoreCase);
}
