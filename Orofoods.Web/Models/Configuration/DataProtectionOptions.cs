namespace Orofoods.Web.Models.Configuration;

public sealed class DataProtectionOptions
{
    public string? ApplicationName { get; set; }
    public string? KeyDirectory { get; set; }
    public AzureDataProtectionOptions? Azure { get; set; }
}

public sealed class AzureDataProtectionOptions
{
    public bool Enabled { get; set; }
    public string? BlobUri { get; set; }
    public string? KeyVaultKeyIdentifier { get; set; }
}
