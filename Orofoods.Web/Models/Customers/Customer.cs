using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orofoods.Web.Models.Customers;

public class Customer
{
    public int Id { get; set; }
    [MaxLength(160)] public string LegalName { get; set; } = "";
    [MaxLength(120)] public string TradeName { get; set; } = "";
    [MaxLength(18)] public string Cnpj { get; set; } = "";
    [MaxLength(30)] public string? WmcCode { get; set; }
    [MaxLength(30)] public string StateRegistration { get; set; } = "";
    [MaxLength(120)] public string ResponsibleName { get; set; } = "";
    [MaxLength(20)] public string ResponsibleDocument { get; set; } = "";
    [MaxLength(160)] public string Email { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(30)] public string WhatsApp { get; set; } = "";
    public CustomerStatus Status { get; set; } = CustomerStatus.Pending;
    public decimal MinimumOrder { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CreditUsed { get; set; }
    public bool CreditOverrideEnabled { get; set; }
    public int? MaximumPaymentTermDays { get; set; }
    public bool CreditBlocked { get; set; }
    public DateTime? CreditReleaseDate { get; set; }
    [MaxLength(1000)] public string? CreditNotes { get; set; }
    public int? PriceTableId { get; set; }
    public PriceTable? PriceTable { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public SalesRepresentative? SalesRepresentative { get; set; }
    public string? InternalSalesUserId { get; set; }
    public ApplicationUser? InternalSalesUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CustomerAddress> Addresses { get; set; } = [];
    public List<ApplicationUser> Users { get; set; } = [];
    public List<CustomerPaymentTerm> CustomerPaymentTerms { get; set; } = [];

    [NotMapped]
    public string PriceTableName => PriceTable?.Name ?? "Sem tabela";

    [NotMapped]
    public string PaymentTerms => CustomerPaymentTerms.Count == 0
        ? "A definir"
        : string.Join(" ou ", CustomerPaymentTerms
            .Where(x => x.IsActive && x.PaymentTerm is not null)
            .OrderBy(x => x.PaymentTerm!.SortOrder)
            .Select(x => x.PaymentTerm!.Name));
}
