using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Services.Commercial;

public sealed class CrmOpportunityService(ApplicationDbContext db, SalesRepresentativeAccessService accessService, UserNotificationService notificationService)
{
    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, int customerId, CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetScopeAsync(user, cancellationToken);
        return await accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).AnyAsync(x => x.Id == customerId, cancellationToken);
    }

    public async Task<List<CrmOpportunity>> ListAsync(ClaimsPrincipal user, int? customerId = null, CrmOpportunityStage? stage = null, CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetScopeAsync(user, cancellationToken);
        var customerQuery = accessService.ApplyCustomerScope(db.Customers.AsNoTracking(), scope).Select(x => x.Id);
        var query = db.CrmOpportunities.AsNoTracking().Include(x => x.Customer).Include(x => x.AssignedUser).Where(x => customerQuery.Contains(x.CustomerId));
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (stage.HasValue) query = query.Where(x => x.Stage == stage);
        return await query.OrderBy(x => x.ExpectedCloseAt).ThenByDescending(x => x.UpdatedAt).Take(100).ToListAsync(cancellationToken);
    }

    public async Task<CrmOpportunity?> GetAsync(ClaimsPrincipal user, int id, CancellationToken cancellationToken = default)
    {
        var opportunity = await db.CrmOpportunities.Include(x => x.Customer).Include(x => x.AssignedUser).Include(x => x.RelatedOrder).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return opportunity is not null && await HasAccessAsync(user, opportunity.CustomerId, cancellationToken) ? opportunity : null;
    }

    public async Task<CrmOpportunity> CreateAsync(ClaimsPrincipal user, int customerId, string title, CrmOpportunityType type, CrmOpportunitySource source, decimal? estimatedValue, DateTime? expectedCloseAt, string? notes, string? assignedUserId, CancellationToken cancellationToken = default)
    {
        if (!await HasAccessAsync(user, customerId, cancellationToken)) throw new UnauthorizedAccessException();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var opportunity = new CrmOpportunity { CustomerId = customerId, AssignedUserId = assignedUserId ?? userId, CreatedByUserId = userId, Title = title.Trim(), Type = type, Source = source, EstimatedValue = estimatedValue, ExpectedCloseAt = expectedCloseAt, Notes = notes, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        opportunity.History.Add(new CrmOpportunityHistory { EventType = CrmOpportunityEventType.Created, ToStage = opportunity.Stage, ChangedByUserId = userId, OccurredAt = DateTime.UtcNow });
        db.CrmOpportunities.Add(opportunity);
        await db.SaveChangesAsync(cancellationToken);
        if (opportunity.AssignedUserId is not null)
        {
            await notificationService.CreateOnceAsync(new UserNotification { UserId = opportunity.AssignedUserId, CustomerId = customerId, OpportunityId = opportunity.Id, Type = UserNotificationType.OpportunityAssigned, Title = "Nova oportunidade atribuída", Message = opportunity.Title, ActionUrl = $"/Admin/Opportunities?customerId={customerId}", DeduplicationKey = $"OPPORTUNITY_ASSIGNED:{opportunity.Id}" }, cancellationToken);
        }
        return opportunity;
    }

    public async Task ChangeStageAsync(ClaimsPrincipal user, int id, CrmOpportunityStage stage, string? lostReason, string? outcome, CancellationToken cancellationToken = default)
    {
        var opportunity = await GetAsync(user, id, cancellationToken) ?? throw new KeyNotFoundException();
        if (stage == CrmOpportunityStage.Lost && string.IsNullOrWhiteSpace(lostReason)) throw new ArgumentException("Motivo da perda é obrigatório.");
        var previous = opportunity.Stage;
        opportunity.Stage = stage;
        opportunity.LostReason = stage == CrmOpportunityStage.Lost ? lostReason : null;
        opportunity.Outcome = outcome;
        opportunity.ClosedAt = stage is CrmOpportunityStage.Won or CrmOpportunityStage.Lost or CrmOpportunityStage.Cancelled ? DateTime.UtcNow : null;
        opportunity.UpdatedAt = DateTime.UtcNow;
        opportunity.History.Add(new CrmOpportunityHistory { EventType = stage == CrmOpportunityStage.Won ? CrmOpportunityEventType.Won : stage == CrmOpportunityStage.Lost ? CrmOpportunityEventType.Lost : CrmOpportunityEventType.StageChanged, FromStage = previous, ToStage = stage, ChangedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier), Notes = lostReason ?? outcome, OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        if (stage == CrmOpportunityStage.Won && opportunity.AssignedUserId is not null)
        {
            await notificationService.CreateOnceAsync(new UserNotification { UserId = opportunity.AssignedUserId, CustomerId = opportunity.CustomerId, OpportunityId = opportunity.Id, Type = UserNotificationType.OpportunityWon, Title = "Oportunidade ganha", Message = opportunity.Title, ActionUrl = $"/Admin/Opportunities?customerId={opportunity.CustomerId}", DeduplicationKey = $"OPPORTUNITY_WON:{opportunity.Id}" }, cancellationToken);
        }
    }
}
