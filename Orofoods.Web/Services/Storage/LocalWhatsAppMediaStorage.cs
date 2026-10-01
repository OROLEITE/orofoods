using Orofoods.Web.Models.Configuration;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Services.Storage;

public sealed class LocalWhatsAppMediaStorage(IWebHostEnvironment environment, StorageOptions options) : IWhatsAppMediaStorage
{
    private readonly string relativeDirectory = string.IsNullOrWhiteSpace(options.Local.WhatsAppMediaPath)
        ? "App_Data/whatsapp-media"
        : options.Local.WhatsAppMediaPath.Trim('/', '\\');

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = WhatsAppMediaPolicy.ExtensionFor(contentType);
        if (extension.Length == 0 || !content.CanSeek || content.Length <= 0)
            throw new InvalidOperationException("Mídia inválida.");

        var directory = ResolveDirectory();
        Directory.CreateDirectory(directory);
        var storageName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = ResolvePath(storageName);
        await using var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        content.Position = 0;
        await content.CopyToAsync(destination, cancellationToken);
        return storageName;
    }

    public Task<WhatsAppMediaReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!IsSafeReference(reference)) return Task.FromResult<WhatsAppMediaReadResult?>(null);
        var fullPath = ResolvePath(reference);
        if (!File.Exists(fullPath)) return Task.FromResult<WhatsAppMediaReadResult?>(null);
        var mimeType = MimeFromExtension(Path.GetExtension(reference));
        if (mimeType is null) return Task.FromResult<WhatsAppMediaReadResult?>(null);
        var info = new FileInfo(fullPath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<WhatsAppMediaReadResult?>(new(stream, mimeType, reference, info.Length));
    }

    private string ResolveDirectory()
    {
        var root = Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var directory = Path.GetFullPath(Path.Combine(root, relativeDirectory));
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de mídia inválido.");
        return directory;
    }

    private string ResolvePath(string reference)
    {
        var directory = ResolveDirectory();
        var path = Path.GetFullPath(Path.Combine(directory, reference));
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Referência de mídia inválida.");
        return path;
    }

    private static bool IsSafeReference(string reference) => !string.IsNullOrWhiteSpace(reference) &&
        reference == Path.GetFileName(reference) && !reference.Contains("..", StringComparison.Ordinal) &&
        WhatsAppMediaPolicy.ExtensionFor(MimeFromExtension(Path.GetExtension(reference)) ?? string.Empty).Length > 0;

    private static string? MimeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp",
        ".aac" => "audio/aac", ".amr" => "audio/amr", ".mp3" => "audio/mpeg", ".m4a" => "audio/mp4", ".ogg" => "audio/ogg",
        ".mp4" => "video/mp4", ".3gp" => "video/3gpp", ".pdf" => "application/pdf", ".txt" => "text/plain",
        ".doc" => "application/msword", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel", ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => null
    };
}
