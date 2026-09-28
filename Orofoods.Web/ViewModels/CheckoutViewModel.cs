using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.ViewModels;

public class CheckoutViewModel
{
    public CartViewModel Cart { get; init; } = new();
    public IReadOnlyList<CustomerAddress> Addresses { get; init; } = [];
    public IReadOnlyList<PaymentTerm> PaymentTerms { get; init; } = [];
    public PaymentEligibilityResult? PaymentEligibility { get; init; }
    public string MercadoPagoPublicKey { get; init; } = "";
    [Range(1, int.MaxValue)] public int AddressId { get; set; }
    [Range(1, int.MaxValue)] public int PaymentTermId { get; set; }
    [DataType(DataType.Date)] public DateTime RequestedDeliveryDate { get; set; } = DateTime.Today.AddDays(1);
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, MaxLength(128)] public string AttemptKey { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(200)] public string? CardToken { get; set; }
    [MaxLength(60)] public string? CardPaymentMethodId { get; set; }
    [Range(1, 24)] public int CardInstallments { get; set; } = 1;
}
