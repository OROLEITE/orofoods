namespace Orofoods.Web.Services.Commercial;

public sealed class WhatsAppBusinessOptions
{
    public const string SectionName = "WhatsAppBusiness";
    public bool Enabled { get; set; }
    public string GraphApiVersion { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string BusinessAccountId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string VerifyToken { get; set; } = "";
    public string WebhookPath { get; set; } = "/api/webhooks/whatsapp";
    public int RequestTimeoutSeconds { get; set; } = 15;
}

public interface IWhatsAppBusinessGateway
{
    Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default);
}

public sealed record WhatsAppSendResult(bool Succeeded, string? ExternalMessageId, string? ErrorCode, string? ErrorMessage);
