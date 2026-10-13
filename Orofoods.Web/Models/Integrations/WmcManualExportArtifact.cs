using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Integrations;

public sealed class WmcManualExportArtifact
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid AttemptId { get; set; }
    [MaxLength(160)] public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
    [MaxLength(64)] public string ContentSha256 { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? CreatedByUserId { get; set; }
    [MaxLength(256)] public string? CreatedByEmail { get; set; }
    public int? SupersedesArtifactId { get; set; }
    public WmcManualExportArtifact? SupersedesArtifact { get; set; }
    [MaxLength(1000)] public string? RegenerationReason { get; set; }
}
