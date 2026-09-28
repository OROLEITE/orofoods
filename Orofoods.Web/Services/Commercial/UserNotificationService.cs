using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;

namespace Orofoods.Web.Services.Commercial;

public sealed class UserNotificationService(ApplicationDbContext db)
{
    public string? CurrentUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public Task<int> UnreadCountAsync(string userId, CancellationToken cancellationToken = default) =>
        db.UserNotifications.CountAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken);

    public Task<List<UserNotification>> RecentAsync(string userId, CancellationToken cancellationToken = default) =>
        db.UserNotifications.AsNoTracking().Include(x => x.Customer).Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var notification = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (notification is null) return false;
        notification.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var notifications = await db.UserNotifications.Where(x => x.UserId == userId && x.ReadAt == null).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var notification in notifications) notification.ReadAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CreateOnceAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification.UserId is null || notification.DeduplicationKey is null) return false;
        if (await db.UserNotifications.AnyAsync(x => x.UserId == notification.UserId && x.DeduplicationKey == notification.DeduplicationKey, cancellationToken)) return false;
        db.UserNotifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
