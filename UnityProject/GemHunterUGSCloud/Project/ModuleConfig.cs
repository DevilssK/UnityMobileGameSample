using GemHunterUGSCloud.Services;
using GemHunterUGSCloud.Purchases;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud
{
public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());

        config.Dependencies.AddSingleton<GiftHeartCooldownService>();
        
        config.Dependencies.AddSingleton<PlayerEconomyService>();
        config.Dependencies.AddSingleton<PlayerDataService>();
        
        
        config.Dependencies.AddSingleton<CommandBatchingService_AreaUpgradables>();
        
        config.Dependencies.AddSingleton<DailyRewardsStatusService>();
        config.Dependencies.AddSingleton<DailyRewardsClaimService>();

        
        config.Dependencies.AddSingleton<PurchaseRedeemer>();
        config.Dependencies.AddSingleton<StoreService>();
    }
}
}
