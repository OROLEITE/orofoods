using Microsoft.AspNetCore.Hosting;
using Orofoods.Web.Models.Configuration;

namespace Orofoods.Web.Services.Storage;

public sealed class LocalProductImageStorage : IProductImageStorage
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _relativeDirectory;

    public LocalProductImageStorage(IWebHostEnvironment environment, StorageOptions? options = null)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        var effectiveOptions = options ?? new StorageOptions();
        _relativeDirectory = string.IsNullOrWhiteSpace(effectiveOptions.Local.ProductImagesPath)
            ? "uploads/products"
            : effectiveOptions.Local.ProductImagesPath.Trim('/');
    }

    public Task<string> SaveAsync(Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        ValidateInput(fileName, contentType, content.Length);

        var extension = Path.GetExtension(fileName);
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var directory = ResolveDirectory();
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, safeFileName);
        using var destination = File.Create(fullPath);
        content.Position = 0;
        content.CopyTo(destination);

        return Task.FromResult($"/{_relativeDirectory.Replace('\\', '/')}/{safeFileName}");
    }

    public Task DeleteAsync(string? url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        var fullPath = ResolveStoredImagePath(url);
        if (fullPath is null)
        {
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public async Task<ProductImageReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveStoredImagePath(reference);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return null;
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
        if (contentType is null)
        {
            return null;
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        await Task.CompletedTask;
        return new ProductImageReadResult(stream, contentType, Path.GetFileName(fullPath));
    }

    public async Task<string> ReplaceAsync(string currentUrl, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        var newUrl = await SaveAsync(content, fileName, contentType, cancellationToken);
        await DeleteAsync(currentUrl, cancellationToken);
        return newUrl;
    }

    private string ResolveDirectory()
    {
        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var resolved = Path.Combine(webRootPath, _relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(resolved);
        var root = Path.GetFullPath(webRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Caminho de imagens invalido.");
        }

        return fullPath;
    }

    private string ResolveFullPath(string relativePath)
    {
        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var relativeSegments = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(webRootPath, relativeSegments));
        var root = Path.GetFullPath(webRootPath);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Caminho de imagem invalido.");
        }

        return target;
    }

    private string? ResolveStoredImagePath(string reference)
    {
        var relativePath = NormalizeRelativePath(reference);
        var directoryPrefix = $"/{_relativeDirectory.Replace('\\', '/')}/";
        const string legacyImagePrefix = "/images/products/";
        var allowedPrefix = relativePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)
            ? directoryPrefix
            : relativePath.StartsWith(legacyImagePrefix, StringComparison.OrdinalIgnoreCase)
                ? legacyImagePrefix
                : null;
        if (string.IsNullOrWhiteSpace(relativePath) || allowedPrefix is null)
        {
            return null;
        }

        var fileName = relativePath[allowedPrefix.Length..];
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
        {
            return null;
        }

        return ResolveFullPath(relativePath);
    }

    private static string NormalizeRelativePath(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var cleaned = url.Trim();
        if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        cleaned = cleaned.Trim();
        if (!cleaned.StartsWith('/'))
        {
            cleaned = $"/{cleaned}";
        }

        return cleaned;
    }

    private static void ValidateInput(string fileName, string? contentType, long length)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new InvalidOperationException("Arquivo de imagem invalido.");
        if (length <= 0 || length > 5 * 1024 * 1024) throw new InvalidOperationException("A imagem deve ter ate 5 MB.");

        var sanitizedFileName = Path.GetFileName(fileName);
        if (!string.Equals(sanitizedFileName, fileName, StringComparison.Ordinal) || fileName.Contains('\\'))
        {
            throw new InvalidOperationException("Nome do arquivo invalido.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
        {
            throw new InvalidOperationException("Use arquivos JPG, PNG ou WEBP.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !IsContentTypeForExtension(contentType, extension))
        {
            throw new InvalidOperationException("Tipo de arquivo invalido.");
        }

        if (fileName.IndexOf("..", StringComparison.Ordinal) >= 0 || fileName.Contains('/') || fileName.Contains('\\'))
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
}
