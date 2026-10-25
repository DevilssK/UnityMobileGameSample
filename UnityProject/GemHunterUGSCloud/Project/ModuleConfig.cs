using GemHunterUGSCloud.Game;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud
{
public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        var gameApiClient = GameApiClient.Create();
        config.Dependencies.AddSingleton(gameApiClient);

        // Holds the Remote Config cache and the guarded transaction: one instance per worker.
        config.Dependencies.AddSingleton(new GemHunterState(gameApiClient));
        config.Dependencies.AddSingleton(GemHunterPurchaseCatalog.Load());
    }
}
}
