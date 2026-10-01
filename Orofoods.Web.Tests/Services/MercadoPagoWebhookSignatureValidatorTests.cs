using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public class MercadoPagoWebhookSignatureValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("online-secret")]
    [InlineData("point-test-secret")]
    public void Accepts_signatures_from_online_or_point_secret(string secret)
    {
        var validator = CreateValidator("online-secret", "point-test-secret");

        Assert.True(validator.IsValid(Sign(secret), "req-123", "MP-ORDER-1"));
    }

    [Fact]
    public void Rejects_signature_when_neither_configured_secret_matches()
    {
        var validator = CreateValidator("online-secret", "point-test-secret");

        Assert.False(validator.IsValid(Sign("unconfigured-secret"), "req-123", "MP-ORDER-1"));
    }

    [Fact]
    public void Rejects_point_signature_when_point_secret_is_not_configured()
    {
        var validator = CreateValidator("online-secret", "");

        Assert.False(validator.IsValid(Sign("point-test-secret"), "req-123", "MP-ORDER-1"));
    }

    private static MercadoPagoWebhookSignatureValidator CreateValidator(string onlineSecret, string pointSecret) => new(
        Options.Create(new MercadoPagoOptions { WebhookSecret = onlineSecret }),
        Options.Create(new MercadoPagoPointOptions { WebhookSecret = pointSecret }),
        new FixedTimeProvider(Now));

    private static string Sign(string secret)
    {
        const string requestId = "req-123";
        const string dataId = "MP-ORDER-1";
        var timestamp = Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{timestamp};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"ts={timestamp},v1={Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest))).ToLowerInvariant()}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
