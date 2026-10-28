using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.ViewModels;

public sealed class SellerCheckoutViewModel
{
    public required int CustomerId { get; init; }
    public string CustomerCode { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string? ErrorMessage { get; set; }
    public CartViewModel Cart { get; init; } = new();
    public IReadOnlyList<PaymentTerm> PaymentTerms { get; init; } = [];
    public PaymentEligibilityResult? PaymentEligibility { get; init; }
    public string MercadoPagoPublicKey { get; init; } = "";
    [Range(1, int.MaxValue)] public int PaymentTermId { get; set; }
    [DataType(DataType.Date)] public DateTime RequestedDeliveryDate { get; set; } = DateTime.Today.AddDays(1);
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, MaxLength(128)] public string AttemptKey { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(200)] public string? CardToken { get; set; }
    [MaxLength(60)] public string? CardPaymentMethodId { get; set; }
    [Range(1, 24)] public int CardInstallments { get; set; } = 1;
}

public sealed record SellerOrderSuccessViewModel(
    int OrderId,
    string OrderNumber,
    string CustomerName,
    decimal Total,
    string PaymentMethod,
    string Status,
    string? FinancialStatus = null);
