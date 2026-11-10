using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Orofoods.Web.Configuration;
using Orofoods.Web.Models.Configuration;
using Orofoods.Web.Services.Commercial;
using DataProtectionOptions = Orofoods.Web.Models.Configuration.DataProtectionOptions;

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

    [Fact]
    public void Production_requires_persistent_encrypted_keys_outside_the_deployment_directory()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "Orofoods.Web");
        var environment = new TestHostEnvironment("Production") { ContentRootPath = contentRoot };
        var unconfigured = new DataProtectionOptions { ApplicationName = "Orofoods.Web" };
        var externalDirectory = new DataProtectionOptions
        {
            ApplicationName = "Orofoods.Web",
            KeyDirectory = Path.Combine(Path.GetTempPath(), "Orofoods.DataProtectionKeys")
        };
        var deploymentDirectory = new DataProtectionOptions
        {
            ApplicationName = "Orofoods.Web",
            KeyDirectory = Path.Combine(contentRoot, "DataProtectionKeys")
        };
        var azureConfiguration = new DataProtectionOptions
        {
            ApplicationName = "Orofoods.Web",
            KeyDirectory = Path.Combine(Path.GetTempPath(), "Orofoods.DataProtectionKeys"),
            Azure = new AzureDataProtectionOptions
            {
                Enabled = true,
                BlobUri = "https://orofoods.blob.core.windows.net/dataprotection/keyring.xml",
                KeyVaultKeyIdentifier = "https://orofoods.vault.azure.net/keys/data-protection"
            }
        };

        Assert.False(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(environment, unconfigured, supportsDpapi: true));
        Assert.True(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(environment, externalDirectory, supportsDpapi: true));
        Assert.False(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(environment, externalDirectory, supportsDpapi: false));
        Assert.False(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(environment, deploymentDirectory, supportsDpapi: true));
        Assert.True(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(environment, azureConfiguration, supportsDpapi: false));

        azureConfiguration.Azure!.BlobUri = "http://orofoods.blob.core.windows.net/dataprotection/keyring.xml";
        Assert.False(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(environment, azureConfiguration, supportsDpapi: true));

        Assert.True(StartupEnvironmentPolicy.IsProductionDataProtectionConfigured(
            new TestHostEnvironment("Development"), unconfigured, supportsDpapi: false));
    }

    [Fact]
    public void Persisted_data_protection_keys_remain_usable_after_provider_recreation()
    {
        var keyDirectory = Path.Combine(Path.GetTempPath(), $"Orofoods.DataProtectionTests.{Guid.NewGuid():N}");
        try
        {
            string protectedValue;
            using (var firstProvider = CreateDataProtectionProvider(keyDirectory))
                protectedValue = firstProvider.GetDataProtector("Orofoods.Web.Tests.Persistence").Protect("persistent-key-check");

            using var restartedProvider = CreateDataProtectionProvider(keyDirectory);
            var restoredValue = restartedProvider.GetDataProtector("Orofoods.Web.Tests.Persistence").Unprotect(protectedValue);

            Assert.Equal("persistent-key-check", restoredValue);
        }
        finally
        {
            var resolvedKeyDirectory = Path.GetFullPath(keyDirectory);
            var resolvedTempDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (string.Equals(Directory.GetParent(resolvedKeyDirectory)?.FullName, resolvedTempDirectory, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(resolvedKeyDirectory))
                Directory.Delete(resolvedKeyDirectory, recursive: true);
        }
    }

    private static ServiceProvider CreateDataProtectionProvider(string keyDirectory) =>
        new ServiceCollection()
            .AddDataProtection()
            .SetApplicationName("Orofoods.Web.Tests")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
            .Services
            .BuildServiceProvider();

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
