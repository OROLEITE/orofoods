using Microsoft.Extensions.Configuration;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class LocalPointTestConfigurationTests
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

    [Theory]
    [InlineData(null, false)]
    [InlineData("synthetic-test-token", true)]
    public void Test_configuration_reports_whether_a_token_is_configured_without_external_secrets(
        string? token,
        bool expectedConfigured)
    {
        var builder = new ConfigurationBuilder();
        builder.AddUserSecretsOnlyForTest(
            "Test",
            typeof(MercadoPagoPointPaymentProvider).Assembly,
            config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MercadoPagoPointOptions.SectionName}:AccessToken"] = token
            }));
        var configuration = builder.Build();
        var tokenConfigured = !string.IsNullOrWhiteSpace(
            configuration[$"{MercadoPagoPointOptions.SectionName}:AccessToken"]);

        Assert.Equal(expectedConfigured, tokenConfigured);
    }
}
