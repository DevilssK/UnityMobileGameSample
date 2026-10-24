using System.Net;
using GemHunterUGSCloud.Purchases;
using Unity.Services.CloudCode.Shared;
using Xunit;
using GoogleStatus = Unity.Services.Economy.Model.PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum;
using AppleStatus = Unity.Services.Economy.Model.PlayerPurchaseAppleappstoreResponseVerification.StatusEnum;

namespace GemHunterUGSCloud.Tests.Purchases;

public class RedemptionStatusMapperTests
{
    [Theory]
    [InlineData(GoogleStatus.VALID, PurchaseRedemptionStatus.Delivered)]
    [InlineData(GoogleStatus.INVALIDALREADYREDEEMED, PurchaseRedemptionStatus.AlreadyDelivered)]
    [InlineData(GoogleStatus.VALIDNOTREDEEMED, PurchaseRedemptionStatus.RetryLater)]
    [InlineData(GoogleStatus.INVALIDANOTHERPLAYER, PurchaseRedemptionStatus.Rejected)]
    [InlineData(GoogleStatus.INVALIDVERIFICATIONFAILED, PurchaseRedemptionStatus.Rejected)]
    [InlineData(GoogleStatus.INVALIDCONFIGURATION, PurchaseRedemptionStatus.Rejected)]
    [InlineData(GoogleStatus.INVALIDPRODUCTIDMISMATCH, PurchaseRedemptionStatus.Rejected)]
    public void FromGoogle_MapsEveryStatus(GoogleStatus status, PurchaseRedemptionStatus expected)
    {
        Assert.Equal(expected, RedemptionStatusMapper.FromGoogle(status));
    }

    [Theory]
    [InlineData(AppleStatus.VALID, PurchaseRedemptionStatus.Delivered)]
    [InlineData(AppleStatus.INVALIDALREADYREDEEMED, PurchaseRedemptionStatus.AlreadyDelivered)]
    [InlineData(AppleStatus.VALIDNOTREDEEMED, PurchaseRedemptionStatus.RetryLater)]
    [InlineData(AppleStatus.INVALIDANOTHERPLAYER, PurchaseRedemptionStatus.Rejected)]
    [InlineData(AppleStatus.INVALIDVERIFICATIONFAILED, PurchaseRedemptionStatus.Rejected)]
    [InlineData(AppleStatus.INVALIDCONFIGURATION, PurchaseRedemptionStatus.Rejected)]
    [InlineData(AppleStatus.INVALIDPRODUCTIDMISMATCH, PurchaseRedemptionStatus.Rejected)]
    public void FromApple_MapsEveryStatus(AppleStatus status, PurchaseRedemptionStatus expected)
    {
        Assert.Equal(expected, RedemptionStatusMapper.FromApple(status));
    }

    [Fact]
    public void FromGoogle_WhenStatusUnknown_ReturnsRejected()
    {
        Assert.Equal(PurchaseRedemptionStatus.Rejected, RedemptionStatusMapper.FromGoogle((GoogleStatus)999));
    }

    [Theory]
    [InlineData(ApiExceptionType.Network)]
    [InlineData(ApiExceptionType.Deserialization)]
    public void FromHttpStatus_WhenNetworkError_ReturnsRetryLater(ApiExceptionType type)
    {
        Assert.Equal(PurchaseRedemptionStatus.RetryLater, RedemptionStatusMapper.FromHttpStatus(null, type));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(429)]
    public void FromHttpStatus_WhenServerErrorOrTooManyRequests_ReturnsRetryLater(int code)
    {
        Assert.Equal(PurchaseRedemptionStatus.RetryLater,
            RedemptionStatusMapper.FromHttpStatus((HttpStatusCode)code, ApiExceptionType.Http));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    public void FromHttpStatus_WhenClientError_ReturnsRejected(int code)
    {
        Assert.Equal(PurchaseRedemptionStatus.Rejected,
            RedemptionStatusMapper.FromHttpStatus((HttpStatusCode)code, ApiExceptionType.Http));
    }

    [Fact]
    public void FromHttpStatus_WhenInvalidParameters_ReturnsRejected()
    {
        Assert.Equal(PurchaseRedemptionStatus.Rejected,
            RedemptionStatusMapper.FromHttpStatus(null, ApiExceptionType.InvalidParameters));
    }
}
