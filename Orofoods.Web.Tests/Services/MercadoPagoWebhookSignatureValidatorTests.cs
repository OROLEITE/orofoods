using Microsoft.Extensions.Options;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public class MercadoPagoWebhookSignatureValidatorTests
{
    private const string Signature = "ts=1789646400000,v1=cb0826b6191645ea428e733091b9869dbceb919d52e65e3d2bbd18c63e591898";

    [Fact]
    public void Accepts_official_hmac_manifest_with_millisecond_timestamp()
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:01:00Z"));

        Assert.True(validator.IsValid(Signature, "req-456", "ORD-AbC-123"));
    }

    [Fact]
    public void Data_id_is_included_exactly_as_received()
    {
        var validator = CreateValidator(DateTimeOffset.Parse("2026-09-17T12:01:00Z"));

        Assert.False(validator.IsValid(Signature, "req-456", "ord-abc-123"));
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

        Assert.False(validator.IsValid(Signature, "req-456", "ORD-AbC-123"));
    }

    private static MercadoPagoWebhookSignatureValidator CreateValidator(DateTimeOffset now) =>
        new(
            Options.Create(new MercadoPagoOptions
            {
                WebhookSecret = "webhook-secret-not-real",
                WebhookSignatureTolerance = TimeSpan.FromMinutes(5)
            }),
            new FixedTimeProvider(now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
