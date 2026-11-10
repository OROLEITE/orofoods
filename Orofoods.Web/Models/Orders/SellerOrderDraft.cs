using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Pricing;

namespace Orofoods.Web.Models.Orders;

public class SellerOrderDraft
{
    public int Id { get; set; }

    [MaxLength(450)]
    public string OwnerUserId { get; set; } = "";
    public ApplicationUser? OwnerUser { get; set; }

    public int SalesRepresentativeId { get; set; }
    public SalesRepresentative? SalesRepresentative { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? CreateRequestKey { get; set; }

    public int? DeliveryAddressId { get; set; }
    public CustomerAddress? DeliveryAddress { get; set; }

    public int? PaymentTermId { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }

    public DateTime? RequestedDeliveryDate { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = "";

    public SellerOrderDraftStatus Status { get; set; } = SellerOrderDraftStatus.Draft;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    public List<SellerOrderDraftItem> Items { get; set; } = [];
    public Order? SubmittedOrder { get; set; }
}
