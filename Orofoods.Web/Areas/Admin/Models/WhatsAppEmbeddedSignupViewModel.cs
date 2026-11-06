namespace Orofoods.Web.Areas.Admin.Models;

public sealed record WhatsAppEmbeddedSignupViewModel(
    bool IsReady,
    string AppId,
    string ConfigurationId,
    string GraphApiVersion,
    string DisplayPhoneNumber,
    string? Message);

public sealed class WhatsAppEmbeddedSignupCompletionRequest
{
    public string? EventType { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? BusinessAccountId { get; set; }
}
