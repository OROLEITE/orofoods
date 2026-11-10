using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Integrations;

public sealed class WmcOrderExportState
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public WmcOrderExportStatus Status { get; set; } = WmcOrderExportStatus.Pending;
    public bool RequiresManualReview { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAt { get; set; }
    public long Version { get; set; } = 1;
    public int? CurrentArtifactId { get; set; }
    public WmcManualExportArtifact? CurrentArtifact { get; set; }
    [MaxLength(450)] public string? ExportedByUserId { get; set; }
    [MaxLength(256)] public string? ExportedByEmail { get; set; }
    public DateTime? ExportedAt { get; set; }
    [MaxLength(160)] public string? FileName { get; set; }
    [MaxLength(450)] public string? ReviewedByUserId { get; set; }
    [MaxLength(256)] public string? ReviewedByEmail { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [MaxLength(450)] public string? ImportedByUserId { get; set; }
    [MaxLength(256)] public string? ImportedByEmail { get; set; }
    public DateTime? ImportedAt { get; set; }
    [MaxLength(2000)] public string? LastError { get; set; }
}
