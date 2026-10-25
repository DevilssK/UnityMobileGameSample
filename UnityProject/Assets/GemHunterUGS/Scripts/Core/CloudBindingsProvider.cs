using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Acts as a centralized provider for the auto-generated Cloud Code bindings (one class per module service).
    /// </summary>
    public class CloudBindingsProvider
    {
        public PlayerDataServiceBindings PlayerData { get; } = new(CloudCodeService.Instance);
        public StoreServiceBindings Store { get; } = new(CloudCodeService.Instance);
        public UpgradableAreaItemsServiceBindings Areas { get; } = new(CloudCodeService.Instance);
        public CommandBatchingService_AreaUpgradablesBindings AreaCompletion { get; } = new(CloudCodeService.Instance);
        public DailyRewardsStatusServiceBindings DailyRewardsStatus { get; } = new(CloudCodeService.Instance);
        public DailyRewardsClaimServiceBindings DailyRewardsClaim { get; } = new(CloudCodeService.Instance);
    }
}
