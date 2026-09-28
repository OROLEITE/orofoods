using System.Globalization;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.ViewModels;

/// <summary>Read-only projection of a persisted PIX payment attempt for the customer-facing confirmation page.</summary>
public sealed class PixPaymentViewModel
{
    private static readonly CultureInfo BrazilianCulture = new("pt-BR");

    public int PaymentId { get; init; }
    public int OrderId { get; init; }
    public PaymentStatus Status { get; init; }
    public string StatusLabel { get; init; } = "";
    public string FormattedAmount { get; init; } = "";
    public DateTime? ExpiresAtLocal { get; init; }
    public string? FormattedExpiration { get; init; }
    public bool HasQrCode { get; init; }
    public string? QrCodeDataUri { get; init; }
    public bool HasCopyPaste { get; init; }
    public string? PixCopyPaste { get; init; }

    public static PixPaymentViewModel FromPayment(Payment payment, TimeZoneInfo localTimeZone)
    {
        var expiresAtLocal = payment.ExpiresAt is { } expiresAt
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc), localTimeZone)
            : (DateTime?)null;

        return new PixPaymentViewModel
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            Status = payment.Status,
            StatusLabel = MapStatusLabel(payment.Status),
            FormattedAmount = payment.Amount.ToString("C", BrazilianCulture),
            ExpiresAtLocal = expiresAtLocal,
            FormattedExpiration = expiresAtLocal?.ToString("dd/MM/yyyy 'às' HH:mm", BrazilianCulture),
            HasQrCode = !string.IsNullOrEmpty(payment.PixQrCodeBase64),
            QrCodeDataUri = string.IsNullOrEmpty(payment.PixQrCodeBase64) ? null : $"data:image/png;base64,{payment.PixQrCodeBase64}",
            HasCopyPaste = !string.IsNullOrEmpty(payment.PixCopyPaste),
            PixCopyPaste = payment.PixCopyPaste
        };
    }

    private static string MapStatusLabel(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Aguardando pagamento",
        PaymentStatus.Approved or PaymentStatus.Paid => "Pagamento confirmado",
        PaymentStatus.Rejected => "Pagamento não aprovado",
        PaymentStatus.Expired => "PIX expirado",
        _ => status.ToString()
    };
}
