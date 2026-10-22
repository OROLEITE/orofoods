namespace Orofoods.Web.Models.Identity;

public sealed class AdminUserChangeAudit
{
    public long Id { get; set; }
    public string ActorUserId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string TargetUserId { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime ChangedAt { get; set; }
}
