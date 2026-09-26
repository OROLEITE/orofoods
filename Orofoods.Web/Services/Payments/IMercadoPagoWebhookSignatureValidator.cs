namespace Orofoods.Web.Services.Payments;

public interface IMercadoPagoWebhookSignatureValidator
{
    bool IsValid(string? signature, string? requestId, string? dataId);
}
