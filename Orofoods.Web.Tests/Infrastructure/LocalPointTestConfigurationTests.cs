using Microsoft.Extensions.Configuration;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class LocalPointTestConfigurationTests(ITestOutputHelper output)
{
    [Fact]
    public void Test_environment_can_explicitly_add_user_secrets()
    {
        var builder = new ConfigurationBuilder();
        var added = false;

        builder.AddUserSecretsOnlyForTest(
            "Test",
            typeof(MercadoPagoPointPaymentProvider).Assembly,
            config =>
            {
                added = true;
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{MercadoPagoPointOptions.SectionName}:AccessToken"] = "synthetic-test-token"
                });
            });

        Assert.True(added);
        var configuration = builder.Build();
        Assert.Equal("synthetic-test-token", configuration[$"{MercadoPagoPointOptions.SectionName}:AccessToken"]);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Staging_and_production_do_not_add_or_depend_on_user_secrets(string environmentName)
    {
        var builder = new ConfigurationBuilder();
        var addUserSecretsWasCalled = false;

        builder.AddUserSecretsOnlyForTest(
            environmentName,
            typeof(MercadoPagoPointPaymentProvider).Assembly,
            _ => addUserSecretsWasCalled = true);

        var configuration = builder.Build();
        Assert.False(addUserSecretsWasCalled);
        Assert.Null(configuration[$"{MercadoPagoPointOptions.SectionName}:AccessToken"]);
    }

    [Fact]
    public void Local_test_configuration_reports_only_whether_point_test_token_is_configured()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecretsOnlyForTest(
                "Test",
                typeof(MercadoPagoPointPaymentProvider).Assembly)
            .Build();
        var tokenConfigured = !string.IsNullOrWhiteSpace(
            configuration[$"{MercadoPagoPointOptions.SectionName}:AccessToken"]);

        output.WriteLine($"MercadoPagoPoint test token configured: {(tokenConfigured ? "YES" : "NO")}");
        Assert.True(tokenConfigured, "MercadoPagoPoint test token configured: NO");
    }
}
