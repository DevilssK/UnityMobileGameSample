using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Config;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Game;

public interface ITuningSource
{
    Task<GemHunterTuning> GetAsync(IExecutionContext context, CancellationToken ct);
}

/// <summary>Reads the four tuning keys from Remote Config (cached by <see cref="RemoteConfigReader"/>).</summary>
public sealed class RemoteConfigTuningSource : ITuningSource
{
    private readonly RemoteConfigReader _reader;

    public RemoteConfigTuningSource(RemoteConfigReader reader)
    {
        _reader = reader;
    }

    public async Task<GemHunterTuning> GetAsync(IExecutionContext context, CancellationToken ct)
    {
        var walletDefaults = _reader.GetAsync(context, GemHunterKeys.WalletDefaultsConfig, ct);
        var areaItems = _reader.GetAsync(context, GemHunterKeys.AreaItemsConfig, ct);
        var areaRewards = _reader.GetAsync(context, GemHunterKeys.AreaCompleteRewardsConfig, ct);
        var dailyRewards = _reader.GetAsync(context, GemHunterKeys.DailyRewardsConfig, ct);
        return GemHunterTuning.From(await walletDefaults, await areaItems, await areaRewards, await dailyRewards);
    }
}
