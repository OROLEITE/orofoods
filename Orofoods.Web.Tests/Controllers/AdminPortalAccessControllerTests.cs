using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminPortalAccessControllerTests
{
    [Fact]
    public async Task Selecting_seller_keeps_admin_session_and_records_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Ana", IsActive = true };
        db.SalesRepresentatives.Add(representative);
        await db.SaveChangesAsync();

        var session = new TestSession();
        var controller = new AdminPortalAccessController(
            db,
            new AdminSellerContextService(),
            new AdminPortalAccessAuditService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin-user"),
                    new Claim(ClaimTypes.Email, "admin@orofoods.local"),
                    new Claim(ClaimTypes.Role, ApplicationRoles.Administrator)
                ], "test")),
                Session = session
            }
        };

        var result = await controller.SelectSeller(representative.Id, null, CancellationToken.None);

        Assert.Equal("/Vendedor", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal(representative.Id, new AdminSellerContextService()
            .GetSelectedRepresentativeId(session, "admin-user"));
        var audit = await db.AdminPortalAccessAudits.SingleAsync();
        Assert.Equal("admin-user", audit.AdministratorUserId);
        Assert.Equal("admin@orofoods.local", audit.AdministratorEmail);
        Assert.Equal(AdminPortalAccessType.SalesRepresentative, audit.AccessType);
        Assert.Equal(representative.Id, audit.TargetId);
        Assert.Equal("Ana", audit.TargetName);
    }

    [Fact]
    public async Task Selecting_inactive_seller_does_not_change_context_or_write_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Inativo", IsActive = false };
        db.SalesRepresentatives.Add(representative);
        await db.SaveChangesAsync();

        var session = new TestSession();
        var controller = new AdminPortalAccessController(db, new AdminSellerContextService(), new AdminPortalAccessAuditService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "admin-user"), new Claim(ClaimTypes.Role, ApplicationRoles.Administrator)], "test")),
                Session = session
            }
        };

        var result = await controller.SelectSeller(representative.Id, null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Null(new AdminSellerContextService().GetSelectedRepresentativeId(session, "admin-user"));
        Assert.Empty(await db.AdminPortalAccessAudits.ToListAsync());
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "admin-portal-access";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
