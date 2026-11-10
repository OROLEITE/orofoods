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
    public bool EmbeddedSignupEnabled { get; set; }
    public string EmbeddedSignupAppId { get; set; } = "";
    public string EmbeddedSignupConfigId { get; set; } = "";
    public string EmbeddedSignupPhoneNumberId { get; set; } = "";
    public string EmbeddedSignupDisplayPhoneNumber { get; set; } = "";
    public string VerifyToken { get; set; } = "";
    public string WebhookPath { get; set; } = "/api/webhooks/whatsapp";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public long MaxInboundMediaBytes { get; set; } = 25 * 1024 * 1024;

    public bool IsEmbeddedSignupReady =>
        Enabled &&
        EmbeddedSignupEnabled &&
        IsGraphApiVersionValid(GraphApiVersion) &&
        !string.IsNullOrWhiteSpace(EmbeddedSignupAppId) &&
        !string.IsNullOrWhiteSpace(EmbeddedSignupConfigId) &&
        !string.IsNullOrWhiteSpace(AppSecret) &&
        !string.IsNullOrWhiteSpace(VerifyToken) &&
        !string.IsNullOrWhiteSpace(EmbeddedSignupPhoneNumberId) &&
        !string.IsNullOrWhiteSpace(EmbeddedSignupDisplayPhoneNumber);

    private static bool IsGraphApiVersionValid(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '.');
}

public interface IWhatsAppBusinessGateway
{
    Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default);
}

public sealed record WhatsAppSendResult(bool Succeeded, string? ExternalMessageId, string? ErrorCode, string? ErrorMessage);

public interface IWhatsAppMediaClient
{
    Task<WhatsAppMediaDownloadResult> DownloadAsync(string mediaId, string expectedMimeType, long maximumBytes, CancellationToken cancellationToken = default);
}

public sealed record WhatsAppMediaDownloadResult(
    bool Succeeded,
    Stream? Content,
    string? MimeType,
    long? SizeBytes,
    string? ErrorCode) : IDisposable
{
    public void Dispose() => Content?.Dispose();
}
