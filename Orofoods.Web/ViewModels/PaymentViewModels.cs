using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.ViewModels;

/// <summary>Authenticated customer requests a PIX attempt. The server derives amount, status, and gateway identifiers.</summary>
public sealed class CreatePixAttemptRequest
{
    [Range(1, int.MaxValue)] public int OrderId { get; set; }
    [Required, MaxLength(128)] public string IdempotencyKey { get; set; } = "";
}

/// <summary>Authenticated customer requests a card attempt. Only the browser-generated token and permitted metadata are accepted; never raw card data.</summary>
public sealed class CreateCardAttemptRequest
{
    [Range(1, int.MaxValue)] public int OrderId { get; set; }
    [Required, MaxLength(128)] public string IdempotencyKey { get; set; } = "";
    [Required, MaxLength(200)] public string CardToken { get; set; } = "";
    [Required, MaxLength(60)] public string PaymentMethodId { get; set; } = "";
    [Range(1, 24)] public int Installments { get; set; } = 1;
}

public sealed record PaymentAttemptResponse(
    int Id,
    int OrderId,
    PaymentMethodType Method,
    PaymentStatus Status,
    decimal Amount,
    string? PixCopyPaste,
    string? PixQrCodeBase64,
    DateTime? ExpiresAt,
    string? CardBrand,
    int? Installments)
{
    public static PaymentAttemptResponse FromPayment(Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Method,
        payment.Status,
        payment.Amount,
        payment.PixCopyPaste,
        payment.PixQrCodeBase64,
        payment.ExpiresAt,
        payment.CardBrand,
        payment.Installments);
}
