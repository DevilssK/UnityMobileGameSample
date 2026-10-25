using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Config;
using DevilCorp.MobileFoundation.Server.State;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.RemoteConfig.Api;
using Unity.Services.RemoteConfig.Model;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class RemoteConfigReaderTests
{
    private readonly IGameApiClient _api = Substitute.For<IGameApiClient>();
    private readonly IRemoteConfigSettingsApi _remoteConfig = Substitute.For<IRemoteConfigSettingsApi>();
    private readonly ManualClock _clock = new();

    public RemoteConfigReaderTests()
    {
        _api.RemoteConfigSettings.Returns(_remoteConfig);
    }

    private IExecutionContext Context(string environmentId)
    {
        var context = Substitute.For<IExecutionContext>();
        context.EnvironmentId.Returns(environmentId);
        context.ProjectId.Returns("project");
        context.AccessToken.Returns("token");
        return context;
    }

    private void Returns(string environmentId, string key, object? value)
    {
        var settings = new Dictionary<string, object>();
        if (value != null)
        {
            settings[key] = value;
        }

        _remoteConfig.AssignSettingsGetAsync(Arg.Any<IExecutionContext>(), "token", "project", environmentId,
                Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Response(new SettingsDeliveryResponse(new Configs(settings), new Metadata(environmentId, "a", "h"))));
    }

    [Fact]
    public async Task GetAsync_TwoEnvironments_DoNotShareCache()
    {
        Returns("dev", "TUNING", JObject.Parse(@"{ ""v"": 1 }"));
        Returns("prod", "TUNING", JObject.Parse(@"{ ""v"": 2 }"));
        var reader = new RemoteConfigReader(_api, _clock);

        var dev = await reader.GetAsync(Context("dev"), "TUNING", CancellationToken.None);
        var prod = await reader.GetAsync(Context("prod"), "TUNING", CancellationToken.None);

        Assert.Equal(1, dev["v"]!.ToObject<int>());
        Assert.Equal(2, prod["v"]!.ToObject<int>());
    }

    [Fact]
    public async Task GetAsync_WithinSixtySeconds_UsesCache()
    {
        Returns("dev", "TUNING", JObject.Parse(@"{ ""v"": 1 }"));
        var reader = new RemoteConfigReader(_api, _clock);

        await reader.GetAsync(Context("dev"), "TUNING", CancellationToken.None);
        _clock.UtcNowUnixMs += 59_000;
        await reader.GetAsync(Context("dev"), "TUNING", CancellationToken.None);
        _clock.UtcNowUnixMs += 2_000;
        await reader.GetAsync(Context("dev"), "TUNING", CancellationToken.None);

        await _remoteConfig.Received(2).AssignSettingsGetAsync(Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_MissingKey_ThrowsConfigurationError()
    {
        Returns("dev", "TUNING", null);
        var reader = new RemoteConfigReader(_api, _clock);

        var error = await Assert.ThrowsAsync<GameOperationException>(() => reader.GetAsync(Context("dev"), "TUNING", CancellationToken.None));

        Assert.Equal(ErrorCodes.ConfigurationError, error.Code);
    }

    private static ApiResponse<T> Response<T>(T data)
    {
        var response = new ApiResponse<T>();
        foreach (var (name, value) in new (string, object?)[] { (nameof(ApiResponse<T>.StatusCode), HttpStatusCode.OK), (nameof(ApiResponse<T>.Data), data) })
        {
            var setter = typeof(ApiResponse<T>).GetProperty(name)!.GetSetMethod(true);
            if (setter != null)
            {
                setter.Invoke(response, new[] { value });
            }
            else
            {
                typeof(ApiResponse<T>).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(response, value);
            }
        }

        return response;
    }
}
