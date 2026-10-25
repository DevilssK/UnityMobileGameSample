using System;
using System.Collections.Generic;
using System.Linq;
using DevilCorp.MobileFoundation.Server.State;

namespace DevilCorp.MobileFoundation.Server.Wallet;

/// <summary>Pure wallet rules. They only change the given state; persisting it is the transaction's job.</summary>
public static class WalletRules
{
    public static WalletState Initial(WalletCatalog catalog, long nowMs) => new()
    {
        CreatedAtUnixMs = nowMs,
        Currencies = catalog.Currencies.ToDictionary(c => c.Key, c => c.Value.Initial),
        Items = catalog.Items.ToDictionary(i => i.Key, i => checked((int)i.Value.Initial)),
    };

    /// <summary>
    /// Adds every grant, or nothing if one is invalid. A timed effect of N units extends its expiry by
    /// N × duration, counted from now if it had already expired.
    /// </summary>
    public static void Credit(WalletState wallet, IEnumerable<Grant> grants, WalletCatalog catalog, long nowUnixSec)
    {
        var list = grants.ToList();
        foreach (var grant in list)
        {
            if (grant.Amount < 0 || catalog.KindOf(grant.Id) == ResourceKind.Unknown)
            {
                throw new GameOperationException(ErrorCodes.ConfigurationError, $"cannot grant {grant.Amount} {grant.Id}");
            }
        }

        foreach (var grant in list)
        {
            switch (catalog.KindOf(grant.Id))
            {
                case ResourceKind.Currency:
                    wallet.Currencies[grant.Id] = checked(wallet.Currencies.GetValueOrDefault(grant.Id) + grant.Amount);
                    break;
                case ResourceKind.Item:
                    wallet.Items[grant.Id] = checked(wallet.Items.GetValueOrDefault(grant.Id) + (int)grant.Amount);
                    break;
                case ResourceKind.TimedEffect:
                    var start = Math.Max(nowUnixSec, wallet.TimedEffects.GetValueOrDefault(grant.Id));
                    wallet.TimedEffects[grant.Id] = start + grant.Amount * catalog.TimedEffects[grant.Id].DurationSeconds;
                    break;
            }
        }
    }

    public static void Spend(WalletState wallet, string currencyId, long amount)
    {
        if (amount < 0)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"cannot spend {amount} {currencyId}");
        }

        var balance = wallet.Currencies.GetValueOrDefault(currencyId);
        if (balance < amount)
        {
            throw new GameOperationException(ErrorCodes.InsufficientFunds, $"need {amount} {currencyId}, have {balance}");
        }

        wallet.Currencies[currencyId] = balance - amount;
    }

    /// <summary>
    /// Records a client operation id. False means it was already applied: the caller must not apply it again.
    /// Only the most recent ids are kept; they protect against retries, not against old replays.
    /// </summary>
    public static bool TryRecordOperation(WalletState wallet, string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, "missing operation id");
        }

        if (wallet.RecentOperations.Contains(operationId))
        {
            return false;
        }

        wallet.RecentOperations.Add(operationId);
        while (wallet.RecentOperations.Count > WalletState.MaxRecentOperations)
        {
            wallet.RecentOperations.RemoveAt(0);
        }

        return true;
    }

    /// <summary>One-time claim. False means it was already claimed.</summary>
    public static bool TryClaim(WalletState wallet, string claimId) => wallet.Claims.Add(claimId);
}
