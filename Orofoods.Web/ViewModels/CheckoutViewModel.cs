using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.ViewModels;

public class CheckoutViewModel
{
    public CartViewModel Cart { get; init; } = new();
    public IReadOnlyList<CustomerAddress> Addresses { get; init; } = [];
    public IReadOnlyList<PaymentTerm> PaymentTerms { get; init; } = [];
    [Range(1, int.MaxValue)] public int AddressId { get; set; }
    [Range(1, int.MaxValue)] public int PaymentTermId { get; set; }
    [DataType(DataType.Date)] public DateTime RequestedDeliveryDate { get; set; } = DateTime.Today.AddDays(1);
    [StringLength(1000)] public string Notes { get; set; } = "";
}
