using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Integrations;

public sealed class WmcProductImportRun
{
    public long Id { get; set; }
    public short BrandCode { get; set; }
    [MaxLength(450)] public string ActorUserId { get; set; } = "";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    [MaxLength(20)] public string Outcome { get; set; } = "";
    [MaxLength(2000)] public string ImportedProductCodes { get; set; } = "";
    [MaxLength(2000)] public string SkippedProductCodes { get; set; } = "";
    [MaxLength(2000)] public string? ErrorSummary { get; set; }
}
