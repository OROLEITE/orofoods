namespace Orofoods.Web.Models.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";
    public LocalStorageOptions Local { get; set; } = new();
    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string ProductImagesPath { get; set; } = "uploads/products";
}

public sealed class AzureBlobStorageOptions
{
    public string ServiceUri { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
}
