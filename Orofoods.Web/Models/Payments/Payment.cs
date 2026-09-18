using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Payments;

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(30)] public string PaymentMethod { get; set; } = "";
    public PaymentMethodType Method { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    [MaxLength(200)] public string? Barcode { get; set; }
    [MaxLength(200)] public string? DigitableLine { get; set; }
    [MaxLength(1000)] public string? BankSlipUrl { get; set; }
    [MaxLength(100)] public string? ExternalPaymentId { get; set; }
    [MaxLength(30)] public string? Gateway { get; set; }
    [MaxLength(100)] public string? GatewayOrderId { get; set; }
    [MaxLength(100)] public string? GatewayPaymentId { get; set; }
    [MaxLength(64)] public string? ExternalReference { get; set; }
    [MaxLength(128)] public string? IdempotencyKey { get; set; }
    [MaxLength(4000)] public string? PixCopyPaste { get; set; }
    public string? PixQrCodeBase64 { get; set; }
    public DateTime? ExpiresAt { get; set; }
    [MaxLength(30)] public string? CardBrand { get; set; }
    [MaxLength(4)] public string? LastFourDigits { get; set; }
    public int? Installments { get; set; }
    [MaxLength(100)] public string? AuthorizationCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
