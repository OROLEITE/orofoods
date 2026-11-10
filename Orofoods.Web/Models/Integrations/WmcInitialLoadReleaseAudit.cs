using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Integrations;

public sealed class WmcInitialLoadReleaseAudit
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    [MaxLength(30)] public string WmcCode { get; set; } = "";
    [Required, MaxLength(450)] public string AdminUserId { get; set; } = "";
    public DateTimeOffset ReleasedAtUtc { get; set; }
    [Required, MaxLength(300)] public string Reason { get; set; } = "";
}
