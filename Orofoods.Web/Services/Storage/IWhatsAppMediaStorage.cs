namespace Orofoods.Web.Services.Storage;

public interface IWhatsAppMediaStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<WhatsAppMediaReadResult?> OpenReadAsync(string reference, CancellationToken cancellationToken = default);
}

public sealed record WhatsAppMediaReadResult(Stream Content, string ContentType, string FileName, long SizeBytes);
