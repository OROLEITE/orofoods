using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Integrations;

public enum WmcExportSource
{
    Manual,
    Automatic
}

public enum WmcExportOutcome
{
    Unknown,
    Generated,
    Available,
    Failed
}

public class WmcExportAudit
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? AttemptId { get; set; }
    public WmcExportSource Source { get; set; } = WmcExportSource.Manual;
    public WmcExportOutcome Outcome { get; set; } = WmcExportOutcome.Unknown;
    [MaxLength(450)] public string? ExportedByUserId { get; set; }
    [MaxLength(256)] public string? ExportedByEmail { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? GeneratedAt { get; set; }
    [MaxLength(160)] public string? FileName { get; set; }
    public bool Succeeded { get; set; }
    [MaxLength(2000)] public string? Error { get; set; }
}
