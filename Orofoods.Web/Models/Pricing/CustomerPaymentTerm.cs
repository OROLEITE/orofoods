namespace Orofoods.Web.Models.Pricing;

public class CustomerPaymentTerm
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int PaymentTermId { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }
    public bool IsActive { get; set; } = true;
}
