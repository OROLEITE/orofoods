using Microsoft.Extensions.Options;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public class MercadoPagoWebhookSignatureValidatorTests
{
    private const string WebhookSecret = "webhook-secret-not-real";
    private const long Timestamp = 1789646400000L;

    [Fact]
    public void Accepts_uppercase_order_id_when_manifest_signature_uses_lowercase_id()
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:01:00Z"));
        var signature = CreateSignature("ORD-AbC-123", "req-456");

        Assert.True(validator.IsValid(signature, "req-456", "ORD-AbC-123"));
    }

    [Fact]
    public void Accepts_numeric_order_id()
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:01:00Z"));

        Assert.True(validator.IsValid(CreateSignature("123456789", "req-456"), "req-456", "123456789"));
    }

    [Fact]
    public void Rejects_invalid_signature_for_uppercase_order_id()
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:01:00Z"));
        var signature = CreateSignature("ORD-AbC-123", "req-456");
        var invalidSignature = signature[..^1] + (signature[^1] == '0' ? "1" : "0");

        Assert.False(validator.IsValid(invalidSignature, "req-456", "ORD-AbC-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1=cb0826b6191645ea428e733091b9869dbceb919d52e65e3d2bbd18c63e591898")]
    [InlineData("ts=1789646400000")]
    [InlineData("ts=not-a-number,v1=cb0826b6191645ea428e733091b9869dbceb919d52e65e3d2bbd18c63e591898")]
    [InlineData("ts=1789646400000,v1=not-hex")]
    public void Rejects_missing_or_malformed_signature(string signature)
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:01:00Z"));

        Assert.False(validator.IsValid(signature, "req-456", "ORD-AbC-123"));
    }

    [Fact]
    public void Rejects_a_valid_signature_outside_the_replay_tolerance()
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:10:01Z"));

        Assert.False(validator.IsValid(CreateSignature("ORD-AbC-123", "req-456"), "req-456", "ORD-AbC-123"));
    }

    private static MercadoPagoWebhookSignatureValidator CreateValidator(DateTimeOffset now) =>
        new(
            Options.Create(new MercadoPagoOptions
            {
                WebhookSecret = WebhookSecret,
                WebhookSignatureTolerance = TimeSpan.FromMinutes(5)
            }),
            new FixedTimeProvider(now));

    private static string CreateSignature(string dataId, string requestId)
    {
        var manifest = $"id:{dataId.Trim().ToLowerInvariant()};request-id:{requestId.Trim()};ts:{Timestamp};";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(WebhookSecret));
        return $"ts={Timestamp},v1={Convert.ToHexStringLower(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(manifest)))}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
