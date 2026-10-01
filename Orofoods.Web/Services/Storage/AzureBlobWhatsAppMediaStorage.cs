using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Orofoods.Web.Models.Configuration;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Services.Storage;

public sealed class AzureBlobWhatsAppMediaStorage : IWhatsAppMediaStorage
{
    private const string Prefix = "whatsapp-media/";
    private readonly BlobContainerClient container;

    public AzureBlobWhatsAppMediaStorage(StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AzureBlob.ServiceUri) || string.IsNullOrWhiteSpace(options.AzureBlob.ContainerName))
            throw new InvalidOperationException("Storage Azure Blob não configurado.");
        var containerUri = new Uri($"{options.AzureBlob.ServiceUri.TrimEnd('/')}/{options.AzureBlob.ContainerName.TrimStart('/')}");
        container = new BlobContainerClient(containerUri, new DefaultAzureCredential());
        container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = WhatsAppMediaPolicy.ExtensionFor(contentType);
        if (extension.Length == 0 || !content.CanSeek || content.Length <= 0)
            throw new InvalidOperationException("Mídia inválida.");
        var reference = $"{Prefix}{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(reference);
        content.Position = 0;
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);
        return reference;
    }

    public async Task<WhatsAppMediaReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!IsSafeReference(reference)) return null;
        var blob = container.GetBlobClient(reference);
        try
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            var mimeType = WhatsAppMediaPolicy.NormalizeMimeType(properties.Value.ContentType);
            if (WhatsAppMediaPolicy.ExtensionFor(mimeType).Length == 0) return null;
            var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
            return new(stream, mimeType, Path.GetFileName(reference), properties.Value.ContentLength);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private static bool IsSafeReference(string reference) => reference.StartsWith(Prefix, StringComparison.Ordinal) &&
        reference.Length > Prefix.Length && !reference.Contains("..", StringComparison.Ordinal) &&
        reference.Count(character => character == '/') == 1 && !reference.Contains('\\');
}
