using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Identity;

public enum AdminPortalAccessType
{
    Customer = 1,
    SalesRepresentative = 2
}

public sealed class AdminPortalAccessAudit
{
    public int Id { get; set; }
    [MaxLength(450)] public string AdministratorUserId { get; set; } = "";
    [MaxLength(256)] public string? AdministratorEmail { get; set; }
    public AdminPortalAccessType AccessType { get; set; }
    public int TargetId { get; set; }
    [MaxLength(200)] public string TargetName { get; set; } = "";
    [MaxLength(45)] public string? IpAddress { get; set; }
    [MaxLength(512)] public string? UserAgent { get; set; }
    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;
}
