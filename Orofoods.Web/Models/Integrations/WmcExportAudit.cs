using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Integrations;

public class WmcExportAudit
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    [MaxLength(450)] public string? ExportedByUserId { get; set; }
    [MaxLength(256)] public string? ExportedByEmail { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(160)] public string? FileName { get; set; }
    public bool Succeeded { get; set; }
    [MaxLength(2000)] public string? Error { get; set; }
}
