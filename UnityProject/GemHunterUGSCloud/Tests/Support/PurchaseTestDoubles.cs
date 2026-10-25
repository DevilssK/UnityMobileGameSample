using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Economy.Api;
using Unity.Services.Economy.Model;

namespace GemHunterUGSCloud.Tests.Support;

internal static class PurchaseTestDoubles
{
    public const string GooglePurchaseData = "{\"orderId\":\"GPA.1\"}";
    public const string GoogleSignature = "sig==";

    public static string GoogleReceipt() =>
        Envelope("GooglePlay", "{\"json\":\"{\\\"orderId\\\":\\\"GPA.1\\\"}\",\"signature\":\"sig==\"}");

    public static string AppleReceipt() => Envelope("AppleAppStore", "MIIT...");

    public static string FakeReceipt() => Envelope("fake", "ThisIsFakeReceiptData");

    public static string Envelope(string store, string payload) =>
        "{\"Store\":\"" + store + "\",\"TransactionID\":\"GPA.1\",\"Payload\":"
        + Newtonsoft.Json.JsonConvert.ToString(payload) + "}";

    public static IExecutionContext Context(string environmentName = "production")
    {
        var context = Substitute.For<IExecutionContext>();
        context.EnvironmentName.Returns(environmentName);
        context.PlayerId.Returns("player-1");
        context.ProjectId.Returns("project-1");
        context.AccessToken.Returns("token");
        context.CorrelationId.Returns("corr-1");
        return context;
    }

    public static (IGameApiClient Client, IEconomyPurchasesApi Purchases) Client()
    {
        var client = Substitute.For<IGameApiClient>();
        var purchases = Substitute.For<IEconomyPurchasesApi>();
        client.EconomyPurchases.Returns(purchases);
        return (client, purchases);
    }

    public static ApiResponse<PlayerPurchaseGoogleplaystoreResponse> GoogleResponse(
        PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum status) =>
        Response(HttpStatusCode.OK, new PlayerPurchaseGoogleplaystoreResponse(
                new PlayerPurchaseGoogleplaystoreResponseVerification(
                    status, new PlayerPurchaseGoogleplaystoreResponseVerificationStore("")),
                EmptyRewards()));

    public static ApiResponse<PlayerPurchaseAppleappstoreResponse> AppleResponse(
        PlayerPurchaseAppleappstoreResponseVerification.StatusEnum status) =>
        Response(HttpStatusCode.OK, new PlayerPurchaseAppleappstoreResponse(
                new PlayerPurchaseAppleappstoreResponseVerification(
                    status, new PlayerPurchaseAppleappstoreResponseVerificationStore("", "", "")),
                EmptyRewards()));

    public static ApiException HttpFailure(HttpStatusCode statusCode, string rawContent = "") =>
        new(ApiExceptionType.Http, "Economy request failed",
            Response<object>(statusCode, null, rawContent));

    public static ApiException NetworkFailure() =>
        new(ApiExceptionType.Network, "Network unreachable", new ApiResponse<object>());

    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;

    public static CancellationToken AnyToken => Arg.Any<CancellationToken>();

    // ApiResponse<T> exposes read-only properties; tests set them by reflection to build canned responses.
    public static ApiResponse<T> Response<T>(HttpStatusCode statusCode, T? data, string rawContent = "")
    {
        var response = new ApiResponse<T>();
        Set(response, nameof(ApiResponse<T>.StatusCode), statusCode);
        Set(response, nameof(ApiResponse<T>.Data), data);
        Set(response, nameof(ApiResponse<T>.RawContent), rawContent);
        return response;
    }

    private static void Set(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName)!;
        var setter = property.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(target, new[] { value });
            return;
        }

        var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        field.SetValue(target, value);
    }

    private static PlayerPurchaseVirtualResponseRewards EmptyRewards() =>
        new(new List<CurrencyExchangeItem>(), new List<InventoryExchangeItem>());
}
