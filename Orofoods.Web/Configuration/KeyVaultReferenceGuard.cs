namespace Orofoods.Web.Configuration;

internal static class KeyVaultReferenceGuard
{
    private const string KeyVaultReferencePrefix = "@Microsoft.KeyVault(";

    internal static string EnsureResolved(string configurationKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration '{configurationKey}' is required.");
        }

        if (value.StartsWith(KeyVaultReferencePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configuration '{configurationKey}' contains an unresolved Azure Key Vault reference.");
        }

        return value;
    }
}
