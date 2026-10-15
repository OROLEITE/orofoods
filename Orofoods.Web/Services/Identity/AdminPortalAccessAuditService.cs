using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace Orofoods.Web.Services.Identity;

public sealed class AdminPortalAccessAuditService(ApplicationDbContext db)
{
    public Task<List<AdminPortalAccessAudit>> GetRecentAsync(CancellationToken cancellationToken = default) =>
        db.AdminPortalAccessAudits.AsNoTracking()
            .OrderByDescending(x => x.SelectedAt)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

    public async Task RecordSelectionAsync(
        string administratorUserId,
        string? administratorEmail,
        AdminPortalAccessType accessType,
        int targetId,
        string targetName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        db.AdminPortalAccessAudits.Add(new AdminPortalAccessAudit
        {
            AdministratorUserId = administratorUserId,
            AdministratorEmail = Limit(administratorEmail, 256),
            AccessType = accessType,
            TargetId = targetId,
            TargetName = Limit(targetName, 200) ?? "",
            IpAddress = Limit(ipAddress, 45),
            UserAgent = Limit(userAgent, 512),
            SelectedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Limit(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength ? value : value[..maximumLength];
}
