using Microsoft.Extensions.Hosting;
using Orofoods.Web.Models.Configuration;

namespace Orofoods.Web.Configuration;

public static class StartupEnvironmentPolicy
{
    public static bool ShouldApplyMigrations(IHostEnvironment environment) =>
        !environment.IsDevelopment() &&
        !environment.IsProduction() &&
        !environment.IsStaging();

    public static bool IsAzureDataProtectionReadyForWhatsApp(
        IHostEnvironment environment,
        bool whatsAppEnabled,
        DataProtectionOptions settings)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(settings);

        if (!environment.IsStaging() || !whatsAppEnabled)
            return true;

        return !string.IsNullOrWhiteSpace(settings.ApplicationName) &&
               settings.Azure is { Enabled: true } azure &&
               IsSecureAzureResourceUri(azure.BlobUri) &&
               IsSecureAzureResourceUri(azure.KeyVaultKeyIdentifier);
    }

    private static bool IsSecureAzureResourceUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);
}
