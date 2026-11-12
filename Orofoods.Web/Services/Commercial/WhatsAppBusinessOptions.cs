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
    public string EmbeddedSignupDisplayPhoneNumber { get; set; } = "";
    public string VerifyToken { get; set; } = "";
    public string WebhookPath { get; set; } = "/api/webhooks/whatsapp";
    public int RequestTimeoutSeconds { get; set; } = 15;
    public long MaxInboundMediaBytes { get; set; } = 25 * 1024 * 1024;

    public bool IsEmbeddedSignupReady =>
        Enabled &&
        EmbeddedSignupEnabled &&
        IsGraphApiVersionValid(GraphApiVersion) &&
        IsMetaIdentifier(EmbeddedSignupAppId) &&
        IsMetaIdentifier(EmbeddedSignupConfigId) &&
        !string.IsNullOrWhiteSpace(AppSecret) &&
        !string.IsNullOrWhiteSpace(VerifyToken) &&
        NormalizePhone(EmbeddedSignupDisplayPhoneNumber).Length > 0;

    private static bool IsMetaIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(char.IsAsciiDigit);

    private static bool IsGraphApiVersionValid(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '.');

    private static string NormalizePhone(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}

public interface IWhatsAppBusinessGateway
{
    Task<WhatsAppSendResult> SendTextAsync(string phoneNumber, string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WhatsAppApprovedTemplate>> GetApprovedTemplatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WhatsAppApprovedTemplate>>([]);

    Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> headerParameters,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? bodyParameterNames = null,
        IReadOnlyList<string>? headerParameterNames = null) =>
        Task.FromResult(new WhatsAppSendResult(false, null, "UNSUPPORTED", "Envio de modelos não está disponível."));
}

public sealed record WhatsAppSendResult(bool Succeeded, string? ExternalMessageId, string? ErrorCode, string? ErrorMessage);

public sealed record WhatsAppApprovedTemplate(
    string Name,
    string Language,
    string Category,
    string? HeaderText,
    string BodyText,
    int HeaderParameterCount,
    int BodyParameterCount,
    IReadOnlyList<string>? HeaderParameterNames = null,
    IReadOnlyList<string>? BodyParameterNames = null);

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
