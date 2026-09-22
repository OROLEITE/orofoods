using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Orofoods.Web.Models.Configuration;

namespace Orofoods.Web.Services.Storage;

public sealed class AzureBlobProductImageStorage : IProductImageStorage
{
    private readonly IAzureBlobContainerClient _containerClient;
    private readonly Uri _containerUri;

    public AzureBlobProductImageStorage(StorageOptions? options = null)
        : this(options, CreateContainerClient(options), ensurePrivateContainer: true)
    {
    }

    internal AzureBlobProductImageStorage(StorageOptions? options, IAzureBlobContainerClient containerClient)
        : this(options, containerClient, ensurePrivateContainer: false)
    {
    }

    private AzureBlobProductImageStorage(StorageOptions? options, IAzureBlobContainerClient containerClient, bool ensurePrivateContainer)
    {
        var effectiveOptions = options ?? new StorageOptions();
        var serviceUri = effectiveOptions.AzureBlob.ServiceUri;
        var containerName = effectiveOptions.AzureBlob.ContainerName;

        if (string.IsNullOrWhiteSpace(serviceUri) || string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("Storage:Provider=AzureBlob exige Storage:AzureBlob:ServiceUri e Storage:AzureBlob:ContainerName configurados.");
        }

        _containerUri = new Uri($"{serviceUri.TrimEnd('/')}/{containerName.TrimStart('/')}" );
        _containerClient = containerClient;
        if (ensurePrivateContainer)
        {
            _containerClient.EnsurePrivate();
        }
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        ValidateInput(fileName, contentType, content.Length);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var safeName = $"{Guid.NewGuid():N}{extension}";
        var blobClient = _containerClient.GetBlobClient(safeName);

        content.Position = 0;
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);
        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        var blobName = GetBlobNameFromUrl(url, _containerUri);
        if (string.IsNullOrWhiteSpace(blobName)) return;

        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public async Task<ProductImageReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        var blobName = GetBlobNameFromUrl(reference, _containerUri);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        var blobClient = _containerClient.GetBlobClient(blobName);
        return await blobClient.OpenReadAsync(cancellationToken);
    }

    public async Task<string> ReplaceAsync(string currentUrl, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        var newUrl = await SaveAsync(content, fileName, contentType, cancellationToken);
        await DeleteAsync(currentUrl, cancellationToken);
        return newUrl;
    }

    private static IAzureBlobContainerClient CreateContainerClient(StorageOptions? options)
    {
        var effectiveOptions = options ?? new StorageOptions();
        var serviceUri = effectiveOptions.AzureBlob.ServiceUri;
        var containerName = effectiveOptions.AzureBlob.ContainerName;
        if (string.IsNullOrWhiteSpace(serviceUri) || string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("Storage:Provider=AzureBlob exige Storage:AzureBlob:ServiceUri e Storage:AzureBlob:ContainerName configurados.");
        }

        var containerUri = new Uri($"{serviceUri.TrimEnd('/')}/{containerName.TrimStart('/')}" );
        return new AzureBlobContainerClientAdapter(new BlobContainerClient(containerUri, new DefaultAzureCredential()));
    }

    private static string GetBlobNameFromUrl(string url, Uri containerUri)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return string.Empty;
        }

        var decodedPath = Uri.UnescapeDataString(uri.AbsolutePath);

        var expectedContainerPath = containerUri.AbsolutePath.TrimEnd('/');
        if (!string.Equals(uri.Scheme, containerUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Host, containerUri.Host, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Port, containerUri.Port)
            || !decodedPath.StartsWith($"{expectedContainerPath}/", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var blobName = decodedPath[(expectedContainerPath.Length + 1)..];
        return blobName.Contains("..", StringComparison.Ordinal)
            || blobName.Contains('\\')
            || blobName.Contains('/')
            || blobName.Contains('%')
            ? string.Empty
            : blobName;
    }

    private static void ValidateInput(string fileName, string? contentType, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidOperationException("Arquivo de imagem invalido.");
        if (length <= 0 || length > 5 * 1024 * 1024) throw new InvalidOperationException("A imagem deve ter ate 5 MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
        {
            throw new InvalidOperationException("Use arquivos JPG, PNG ou WEBP.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !IsContentTypeForExtension(contentType, extension))
        {
            throw new InvalidOperationException("Tipo de arquivo invalido.");
        }

        var sanitizedFileName = Path.GetFileName(fileName);
        if (!string.Equals(sanitizedFileName, fileName, StringComparison.Ordinal) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
        {
            throw new InvalidOperationException("Nome do arquivo invalido.");
        }
    }

    private static bool IsContentTypeForExtension(string contentType, string extension) => extension switch
    {
        ".jpg" or ".jpeg" => string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase),
        ".png" => string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase),
        ".webp" => string.Equals(contentType, "image/webp", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    internal interface IAzureBlobContainerClient
    {
        IAzureBlobClient GetBlobClient(string blobName);
        void EnsurePrivate();
    }

    internal interface IAzureBlobClient
    {
        Uri Uri { get; }
        Task UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken);
        Task<ProductImageReadResult?> OpenReadAsync(CancellationToken cancellationToken);
        Task DeleteIfExistsAsync(DeleteSnapshotsOption snapshots, CancellationToken cancellationToken);
    }

    private sealed class AzureBlobContainerClientAdapter(BlobContainerClient client) : IAzureBlobContainerClient
    {
        public IAzureBlobClient GetBlobClient(string blobName) => new AzureBlobClientAdapter(client.GetBlobClient(blobName));

        public void EnsurePrivate()
        {
            client.CreateIfNotExists(PublicAccessType.None);
            client.SetAccessPolicy(PublicAccessType.None);
        }
    }

    private sealed class AzureBlobClientAdapter(BlobClient client) : IAzureBlobClient
    {
        public Uri Uri => client.Uri;

        public async Task<ProductImageReadResult?> OpenReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                var properties = await client.GetPropertiesAsync(cancellationToken: cancellationToken);
                var contentType = GetValidatedContentType(client.Uri.AbsolutePath, properties.Value.ContentType);
                if (contentType is null)
                {
                    return null;
                }

                var stream = await client.OpenReadAsync(cancellationToken: cancellationToken);
                return new ProductImageReadResult(stream, contentType, Path.GetFileName(client.Uri.AbsolutePath));
            }
            catch (Azure.RequestFailedException exception) when (exception.Status == 404)
            {
                return null;
            }
        }

        public async Task UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken)
        {
            await client.UploadAsync(content, options, cancellationToken);
        }

        public async Task DeleteIfExistsAsync(DeleteSnapshotsOption snapshots, CancellationToken cancellationToken)
        {
            await client.DeleteIfExistsAsync(snapshots, cancellationToken: cancellationToken);
        }
    }

    private static string? GetValidatedContentType(string path, string? storedContentType)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var expected = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        return expected is not null
            && (string.IsNullOrWhiteSpace(storedContentType)
                || string.Equals(storedContentType, expected, StringComparison.OrdinalIgnoreCase))
            ? expected
            : null;
    }
}
