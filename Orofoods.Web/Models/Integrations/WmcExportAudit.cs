using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Integrations;

public class WmcExportAudit
{
    public const string AutomatedAttemptMarker = "AUTOMACAO_WMC";

    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? AttemptId { get; set; }
    public WmcExportSource Source { get; set; } = WmcExportSource.Manual;
    public WmcExportOutcome Outcome { get; set; } = WmcExportOutcome.Failed;
    [MaxLength(450)] public string? ExportedByUserId { get; set; }
    [MaxLength(256)] public string? ExportedByEmail { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
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

public enum WmcExportSource
{
    Manual,
    Automatic
}

public enum WmcExportOutcome
{
    Started,
    Succeeded,
    Failed,
    Unknown
}
