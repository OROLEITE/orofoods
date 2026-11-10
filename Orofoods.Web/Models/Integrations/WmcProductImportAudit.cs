namespace Orofoods.Web.Models.Integrations;

public sealed class WmcProductImportAudit
{
    public int Id { get; set; }
    public short BrandCode { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Status { get; set; } = "Succeeded";
    public int Selected { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Errors { get; set; }
    public string? ErrorMessage { get; set; }
    public string ResultsJson { get; set; } = "[]";
}
