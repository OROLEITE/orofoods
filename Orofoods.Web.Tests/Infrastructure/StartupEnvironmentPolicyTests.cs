using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Orofoods.Web.Configuration;
using Orofoods.Web.Models.Configuration;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class StartupEnvironmentPolicyTests
{
    [Theory]
    [InlineData("Development", false)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    [InlineData("Testing", true)]
    [InlineData("Custom", true)]
    public void Automatic_migrations_skip_staging_and_preserve_other_environment_behavior(string environmentName, bool expected)
    {
        Assert.Equal(expected, StartupEnvironmentPolicy.ShouldApplyMigrations(new TestHostEnvironment(environmentName)));
    }

    [Fact]
    public void Staging_requires_azure_persistent_data_protection_only_when_embedded_signup_is_enabled()
    {
        var environment = new TestHostEnvironment("Staging");
        var missingAzureConfiguration = new DataProtectionOptions
        {
            ApplicationName = "Orofoods.Web.Staging",
            Azure = new AzureDataProtectionOptions()
        };
        var readyConfiguration = new DataProtectionOptions
        {
            ApplicationName = "Orofoods.Web.Staging",
            Azure = new AzureDataProtectionOptions
            {
                Enabled = true,
                BlobUri = "https://orofoodsstg.blob.core.windows.net/dataprotection/keyring.xml",
                KeyVaultKeyIdentifier = "https://orofoods-stg.vault.azure.net/keys/data-protection"
            }
        };

        var legacyCloudApiOptions = new WhatsAppBusinessOptions { Enabled = true };
        Assert.False(legacyCloudApiOptions.EmbeddedSignupEnabled);
        Assert.True(StartupEnvironmentPolicy.IsAzureDataProtectionReadyForEmbeddedSignup(
            environment, legacyCloudApiOptions.Enabled, legacyCloudApiOptions.EmbeddedSignupEnabled, missingAzureConfiguration));
        Assert.True(StartupEnvironmentPolicy.IsAzureDataProtectionReadyForEmbeddedSignup(
            environment, whatsAppEnabled: false, embeddedSignupEnabled: true, settings: missingAzureConfiguration));
        Assert.False(StartupEnvironmentPolicy.IsAzureDataProtectionReadyForEmbeddedSignup(
            environment, true, true, missingAzureConfiguration));
        Assert.True(StartupEnvironmentPolicy.IsAzureDataProtectionReadyForEmbeddedSignup(
            environment, true, true, readyConfiguration));
    }

    [Theory]
    [InlineData("http://orofoodsstg.blob.core.windows.net/dataprotection/keyring.xml", "https://orofoods-stg.vault.azure.net/keys/data-protection")]
    [InlineData("https://orofoodsstg.blob.core.windows.net/dataprotection/keyring.xml?sig=secret", "https://orofoods-stg.vault.azure.net/keys/data-protection")]
    [InlineData("https://orofoodsstg.blob.core.windows.net/dataprotection/keyring.xml", "http://orofoods-stg.vault.azure.net/keys/data-protection")]
    public void Staging_rejects_insecure_or_secret_bearing_data_protection_uris(string blobUri, string keyIdentifier)
    {
        var configuration = new DataProtectionOptions
        {
            ApplicationName = "Orofoods.Web.Staging",
            Azure = new AzureDataProtectionOptions
            {
                Enabled = true,
                BlobUri = blobUri,
                KeyVaultKeyIdentifier = keyIdentifier
            }
        };

        Assert.False(StartupEnvironmentPolicy.IsAzureDataProtectionReadyForEmbeddedSignup(
            new TestHostEnvironment("Staging"), true, true, configuration));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
