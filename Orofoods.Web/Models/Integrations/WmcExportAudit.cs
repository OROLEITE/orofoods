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
    Failed,
    Rejected,
    Regenerated,
    Imported,
    Started,
    Succeeded
}

public class WmcExportAudit
{
    public const string AutomatedAttemptMarker = "AUTOMACAO_WMC";

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
    public int? HttpStatusCode { get; set; }
    [MaxLength(2000)] public string? ResponseBody { get; set; }
    public int? WmcTransmissionId { get; set; }
    public int? WmcSalesRepresentativeCode { get; set; }
    [MaxLength(2000)] public string? WmcDestination { get; set; }
    public int? NroPedidoHttpStatusCode { get; set; }
    [MaxLength(2000)] public string? NroPedidoResponseBody { get; set; }
    [MaxLength(2000)] public string? NroPedidoError { get; set; }
}
