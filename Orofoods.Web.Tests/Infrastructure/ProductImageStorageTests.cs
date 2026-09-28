using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Moq;
using Orofoods.Web.Models.Configuration;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class ProductImageStorageTests
{
    [Fact]
    public async Task LocalProvider_saves_a_safe_url_and_does_not_use_the_original_filename()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orofoods-product-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.WebRootPath).Returns(tempRoot);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Provider"] = "Local",
                    ["Storage:Local:ProductImagesPath"] = "uploads/products"
                })
                .Build();

            var storageSettings = config.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
            var storage = new LocalProductImageStorage(environment.Object, storageSettings);
            await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });

            var url = await storage.SaveAsync(stream, "product-photo.jpg", "image/jpeg");

            Assert.StartsWith("/uploads/products/", url);
            Assert.DoesNotContain("product-photo", url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("..", url, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(tempRoot, "uploads", "products", Path.GetFileName(url.TrimStart('/')))));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LocalProvider_rejects_invalid_extension_and_path_traversal()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orofoods-product-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.WebRootPath).Returns(tempRoot);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Provider"] = "Local",
                    ["Storage:Local:ProductImagesPath"] = "uploads/products"
                })
                .Build();

            var storageSettings = config.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
            var storage = new LocalProductImageStorage(environment.Object, storageSettings);
            await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(stream, "file.exe", "application/octet-stream"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "../../../../file.jpg", "image/jpeg"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "file.png", "image/jpeg"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LocalProvider_delete_and_replace_keep_the_last_safe_reference()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orofoods-product-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.WebRootPath).Returns(tempRoot);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Provider"] = "Local",
                    ["Storage:Local:ProductImagesPath"] = "uploads/products"
                })
                .Build();

            var storageSettings = config.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
            var storage = new LocalProductImageStorage(environment.Object, storageSettings);
            var first = await storage.SaveAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "first.jpg", "image/jpeg");

            var replacement = await storage.ReplaceAsync(first, new MemoryStream(new byte[] { 9, 8, 7 }), "second.png", "image/png");

            Assert.StartsWith("/uploads/products/", replacement);
            Assert.NotEqual(first, replacement);
            Assert.False(File.Exists(Path.Combine(tempRoot, "uploads", "products", Path.GetFileName(first.TrimStart('/')))));
            Assert.True(File.Exists(Path.Combine(tempRoot, "uploads", "products", Path.GetFileName(replacement.TrimStart('/')))));

            await storage.DeleteAsync(replacement);
            Assert.False(File.Exists(Path.Combine(tempRoot, "uploads", "products", Path.GetFileName(replacement.TrimStart('/')))));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LocalProvider_reads_only_configured_images_with_validated_content_type()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orofoods-product-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.WebRootPath).Returns(tempRoot);
            var storage = new LocalProductImageStorage(environment.Object, new StorageOptions());
            var reference = await storage.SaveAsync(new MemoryStream([1, 2, 3]), "read.png", "image/png");

            var result = await storage.OpenReadAsync(reference);

            Assert.NotNull(result);
            Assert.Equal("image/png", result!.ContentType);
            Assert.Equal(".png", Path.GetExtension(result.FileName));
            await result.Content.DisposeAsync();
            Directory.CreateDirectory(Path.Combine(tempRoot, "images", "products"));
            await File.WriteAllBytesAsync(Path.Combine(tempRoot, "images", "products", "legacy.jpg"), [4, 5]);
            var legacy = await storage.OpenReadAsync("/images/products/legacy.jpg");
            Assert.NotNull(legacy);
            Assert.Equal("image/jpeg", legacy!.ContentType);
            await legacy.Content.DisposeAsync();
            Assert.Null(await storage.OpenReadAsync("/uploads/products/../../appsettings.json"));
            Assert.Null(await storage.OpenReadAsync("https://external.example/uploads/products/image.png"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LocalProvider_rejects_a_configured_directory_outside_web_root()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "orofoods-product-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.WebRootPath).Returns(tempRoot);
            var storage = new LocalProductImageStorage(environment.Object, new StorageOptions
            {
                Local = new LocalStorageOptions { ProductImagesPath = "../../outside" }
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                storage.SaveAsync(new MemoryStream([1]), "image.jpg", "image/jpeg"));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AzureProvider_requires_complete_configuration()
    {
        var options = new StorageOptions
        {
            Provider = "AzureBlob",
            AzureBlob = new AzureBlobStorageOptions { ServiceUri = "", ContainerName = "" }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new AzureBlobProductImageStorage(options));
        Assert.Contains("Storage:Provider=AzureBlob", exception.Message);
    }

    [Fact]
    public async Task AzureProvider_uploads_with_content_type_and_returns_blob_uri()
    {
        var options = new StorageOptions
        {
            Provider = "AzureBlob",
            AzureBlob = new AzureBlobStorageOptions
            {
                ServiceUri = "https://storofoodsstg01.blob.core.windows.net",
                ContainerName = "product-images"
            }
        };
        var blob = new FakeBlobClient(new Uri("https://storofoodsstg01.blob.core.windows.net/product-images/image.jpg"));
        var container = new FakeBlobContainerClient(blob);

        var storage = new AzureBlobProductImageStorage(options, container);
        var url = await storage.SaveAsync(new MemoryStream(new byte[] { 1, 2, 3 }), "product.jpg", "image/jpeg");

        Assert.Equal("https://storofoodsstg01.blob.core.windows.net/product-images/image.jpg", url);
        Assert.NotNull(blob.UploadOptions);
        Assert.Equal("image/jpeg", blob.UploadOptions!.HttpHeaders!.ContentType);
    }

    [Fact]
    public async Task AzureProvider_reads_a_blob_through_the_fake_client_without_exposing_sdk_types()
    {
        var options = new StorageOptions
        {
            Provider = "AzureBlob",
            AzureBlob = new AzureBlobStorageOptions
            {
                ServiceUri = "https://storofoodsstg01.blob.core.windows.net",
                ContainerName = "product-images"
            }
        };
        var blob = new FakeBlobClient(
            new Uri("https://storofoodsstg01.blob.core.windows.net/product-images/image.webp"),
            new ProductImageReadResult(new MemoryStream([1, 2]), "image/webp", "image.webp"));
        var storage = new AzureBlobProductImageStorage(options, new FakeBlobContainerClient(blob));

        var result = await storage.OpenReadAsync(blob.Uri.ToString());

        Assert.NotNull(result);
        Assert.Equal("image/webp", result!.ContentType);
        await result.Content.DisposeAsync();
    }

    [Fact]
    public async Task AzureProvider_does_not_delete_a_blob_outside_the_configured_private_container()
    {
        var options = new StorageOptions
        {
            Provider = "AzureBlob",
            AzureBlob = new AzureBlobStorageOptions
            {
                ServiceUri = "https://storofoodsstg01.blob.core.windows.net",
                ContainerName = "product-images"
            }
        };
        var blob = new FakeBlobClient(new Uri("https://storofoodsstg01.blob.core.windows.net/product-images/image.jpg"));
        var container = new FakeBlobContainerClient(blob);

        var storage = new AzureBlobProductImageStorage(options, container);
        await storage.DeleteAsync("https://other-account.blob.core.windows.net/product-images/image.jpg");
        await storage.DeleteAsync("https://storofoodsstg01.blob.core.windows.net/product-images/%2e%2e%2fsecret.jpg");
        await storage.DeleteAsync("https://storofoodsstg01.blob.core.windows.net/product-images/%252e%252e%252fsecret.jpg");

        Assert.False(container.GetBlobClientCalled);
        Assert.False(blob.DeleteCalled);
    }

    [Fact]
    public async Task AzureProvider_replace_uploads_new_content_and_deletes_the_previous_reference()
    {
        var options = new StorageOptions
        {
            Provider = "AzureBlob",
            AzureBlob = new AzureBlobStorageOptions
            {
                ServiceUri = "https://storofoodsstg01.blob.core.windows.net",
                ContainerName = "product-images"
            }
        };
        var blob = new FakeBlobClient(new Uri("https://storofoodsstg01.blob.core.windows.net/product-images/new.jpg"));
        var storage = new AzureBlobProductImageStorage(options, new FakeBlobContainerClient(blob));

        var replacement = await storage.ReplaceAsync(
            "https://storofoodsstg01.blob.core.windows.net/product-images/old.jpg",
            new MemoryStream([9, 8]),
            "new.jpg",
            "image/jpeg");

        Assert.Equal(blob.Uri.ToString(), replacement);
        Assert.True(blob.UploadOptions is not null);
        Assert.True(blob.DeleteCalled);
    }

    private sealed class FakeBlobContainerClient(FakeBlobClient blob) : AzureBlobProductImageStorage.IAzureBlobContainerClient
    {
        public bool GetBlobClientCalled { get; private set; }

        public AzureBlobProductImageStorage.IAzureBlobClient GetBlobClient(string blobName)
        {
            GetBlobClientCalled = true;
            return blob;
        }

        public void EnsurePrivate() => throw new InvalidOperationException("Test client must not initialize a remote container.");
    }

    private sealed class FakeBlobClient(Uri uri, ProductImageReadResult? readResult = null) : AzureBlobProductImageStorage.IAzureBlobClient
    {
        public Uri Uri { get; } = uri;
        public BlobUploadOptions? UploadOptions { get; private set; }
        public bool DeleteCalled { get; private set; }

        public Task<ProductImageReadResult?> OpenReadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(readResult);
        }

        public Task UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken)
        {
            UploadOptions = options;
            return Task.CompletedTask;
        }

        public Task DeleteIfExistsAsync(DeleteSnapshotsOption snapshots, CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Storage_provider_selection_uses_local_by_default_and_reads_typed_options()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "Local",
                ["Storage:Local:ProductImagesPath"] = "uploads/products"
            })
            .Build();

        var options = config.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        Assert.Equal("Local", options.Provider);
        Assert.Equal("uploads/products", options.Local.ProductImagesPath);
    }

    [Fact]
    public void ProductsController_does_not_manipulate_filesystem_directly()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Orofoods.Web", "Areas", "Admin", "Controllers", "ProductsController.cs"));

        Assert.DoesNotContain("Directory.CreateDirectory", source);
        Assert.DoesNotContain("File.Create", source);
        Assert.DoesNotContain("File.Delete", source);
        Assert.DoesNotContain("WebRootPath", source);
    }
}
