using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Pricing;

public class PaymentTerm
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CustomerPaymentTerm> CustomerPaymentTerms { get; set; } = [];
}
