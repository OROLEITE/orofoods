using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Models.Orders;

public class Order
{
    private string notes = string.Empty;

    public int Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(450)] public string CreatedByUserId { get; set; } = "";
    public ApplicationUser? CreatedByUser { get; set; }
    public int? DeliveryAddressId { get; set; }
    public CustomerAddress? DeliveryAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RequestedDeliveryDate { get; set; }
    public int? PaymentTermId { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Freight { get; set; }
    public decimal Total { get; set; }
    [MaxLength(40)] public string PaymentMethod { get; set; } = "PIX";
    [MaxLength(500)] public string Notes
    {
        get => notes;
        set => notes = value ?? string.Empty;
    }
    public DateTime? ConfirmedAt { get; set; }
    [MaxLength(100)] public string? ExternalOrderId { get; set; }
    [MaxLength(100)] public string? ErpOrderNumber { get; set; }
    public IntegrationStatus IntegrationStatus { get; set; } = IntegrationStatus.Pending;
    public DateTime? LastIntegrationAttempt { get; set; }
    [MaxLength(2000)] public string? IntegrationError { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public List<OrderStatusHistory> StatusHistory { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<WmcExportAudit> WmcExportAudits { get; set; } = [];
}
