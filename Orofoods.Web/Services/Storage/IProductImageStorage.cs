namespace Orofoods.Web.Services.Storage;

public interface IProductImageStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default);
    Task<ProductImageReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? url, CancellationToken cancellationToken = default);
    Task<string> ReplaceAsync(string currentUrl, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default);
}

public sealed record ProductImageReadResult(Stream Content, string ContentType, string FileName);
