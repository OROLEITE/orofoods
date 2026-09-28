using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CrmOpportunityAndNotificationTests
{
    [Fact]
    public async Task OpportunityCreationRequiresCustomerScopeAndCanBeWon()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = new ApplicationUser { Id = "internal", UserName = "internal", Email = "internal@test.local", IsActive = true };
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, InternalSalesUserId = user.Id };
        db.AddRange(user, customer);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Role, "Vendedor")], "test"));
        var service = new CrmOpportunityService(db, new SalesRepresentativeAccessService(db), new UserNotificationService(db));

        var opportunity = await service.CreateAsync(principal, customer.Id, "Recompra", CrmOpportunityType.Repurchase, CrmOpportunitySource.RepurchaseAlert, 100m, DateTime.UtcNow.AddDays(3), null, null);
        await service.ChangeStageAsync(principal, opportunity.Id, CrmOpportunityStage.Won, null, "Pedido confirmado");

        var saved = await db.CrmOpportunities.Include(x => x.History).SingleAsync();
        Assert.Equal(CrmOpportunityStage.Won, saved.Stage);
        Assert.NotNull(saved.ClosedAt);
        Assert.Equal(user.Id, saved.AssignedUserId);
        Assert.Contains(saved.History, x => x.EventType == CrmOpportunityEventType.Won);
    }

    [Fact]
    public async Task NotificationIsOwnedAndDeduplicated()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = new UserNotificationService(db);
        var notification = new UserNotification { UserId = "internal", Type = UserNotificationType.RepurchaseDue, Title = "Recompra", Message = "Cliente", DeduplicationKey = "REPURCHASE:1:2026-09-18" };

        Assert.True(await service.CreateOnceAsync(notification));
        Assert.False(await service.CreateOnceAsync(new UserNotification { UserId = "internal", Type = notification.Type, Title = notification.Title, Message = notification.Message, DeduplicationKey = notification.DeduplicationKey }));
        Assert.False(await service.MarkReadAsync("other-user", notification.Id));
        Assert.True(await service.MarkReadAsync("internal", notification.Id));
    }
}
