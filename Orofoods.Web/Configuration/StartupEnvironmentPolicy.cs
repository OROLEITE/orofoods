using Microsoft.Extensions.Hosting;
using Orofoods.Web.Models.Configuration;

namespace Orofoods.Web.Configuration;

public static class StartupEnvironmentPolicy
{
    public static bool ShouldApplyMigrations(IHostEnvironment environment) =>
        !environment.IsDevelopment() &&
        !environment.IsProduction() &&
        !environment.IsStaging();

    public static bool IsAzureDataProtectionReadyForEmbeddedSignup(
        IHostEnvironment environment,
        bool whatsAppEnabled,
        bool embeddedSignupEnabled,
        DataProtectionOptions settings)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(settings);

        if (!environment.IsStaging() || !whatsAppEnabled || !embeddedSignupEnabled)
            return true;

        return !string.IsNullOrWhiteSpace(settings.ApplicationName) &&
               settings.Azure is { Enabled: true } azure &&
               IsSecureAzureResourceUri(azure.BlobUri) &&
               IsSecureAzureResourceUri(azure.KeyVaultKeyIdentifier);
    }

    public static bool IsProductionDataProtectionConfigured(
        IHostEnvironment environment,
        DataProtectionOptions settings,
        bool supportsDpapi)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(settings);

        if (!environment.IsProduction())
            return true;

        if (string.IsNullOrWhiteSpace(settings.ApplicationName))
            return false;

        if (settings.Azure is { Enabled: true } azure)
            return IsSecureAzureResourceUri(azure.BlobUri) &&
                   IsSecureAzureResourceUri(azure.KeyVaultKeyIdentifier);

        return supportsDpapi && IsExternalAbsoluteDirectory(settings.KeyDirectory, environment.ContentRootPath);
    }

    private static bool IsExternalAbsoluteDirectory(string? directory, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            return false;

        try
        {
            var relativePath = Path.GetRelativePath(Path.GetFullPath(contentRoot), Path.GetFullPath(directory));
            return Path.IsPathRooted(relativePath) ||
                   relativePath == ".." ||
                   relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                   relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSecureAzureResourceUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);
}
