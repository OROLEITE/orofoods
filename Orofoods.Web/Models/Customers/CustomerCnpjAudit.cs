using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Customers;

public enum CustomerCnpjAuditAction
{
    CustomerUpdated = 1,
    OrderSnapshotCorrected = 2
}

public sealed class CustomerCnpjAudit
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public CustomerCnpjAuditAction Action { get; set; }
    [MaxLength(18)] public string? PreviousCnpj { get; set; }
    [MaxLength(18)] public string NewCnpj { get; set; } = "";
    [MaxLength(1000)] public string Reason { get; set; } = "";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string ChangedByUserId { get; set; } = "";
    [MaxLength(256)] public string? ChangedByEmail { get; set; }
}
