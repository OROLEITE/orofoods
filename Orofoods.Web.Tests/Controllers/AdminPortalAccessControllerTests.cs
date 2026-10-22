using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
    public async Task Recent_access_audit_returns_five_latest_rows_with_deterministic_ties()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var latest = DateTime.UtcNow.Date.AddHours(12);
        db.AdminPortalAccessAudits.AddRange(Enumerable.Range(1, 7).Select(index => new AdminPortalAccessAudit
        {
            Id = index,
            AdministratorUserId = "admin-user",
            AccessType = AdminPortalAccessType.Customer,
            TargetId = index,
            TargetName = $"Cliente {index}",
            SelectedAt = index >= 5 ? latest : latest.AddHours(-index)
        }));
        await db.SaveChangesAsync();

        var result = await new AdminPortalAccessAuditService(db).GetRecentAsync();

        Assert.Equal(new[] { 7, 6, 5, 1, 2 }, result.Select(item => item.Id));
        Assert.Equal(7, await db.AdminPortalAccessAudits.CountAsync());
    }

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
            new CustomerAccessService(db),
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
        var controller = new AdminPortalAccessController(db, new CustomerAccessService(db), new AdminSellerContextService(), new AdminPortalAccessAuditService(db));
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

    [Fact]
    public async Task Customer_search_endpoint_returns_only_active_approved_suggestions_and_is_admin_protected()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var approved = new Customer
        {
            LegalName = "Cliente Aprovado Ltda", TradeName = "Cliente Aprovado", Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved, IsActive = true
        };
        var pending = new Customer
        {
            LegalName = "Cliente Pendente Ltda", TradeName = "Cliente Pendente", Cnpj = "98.765.432/0001-99",
            Status = CustomerStatus.Pending, IsActive = true
        };
        db.AddRange(approved, pending);
        await db.SaveChangesAsync();
        var controller = new AdminPortalAccessController(
            db, new CustomerAccessService(db), new AdminSellerContextService(), new AdminPortalAccessAuditService(db));

        var result = await controller.SearchCustomers("Cliente", CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var suggestions = Assert.IsAssignableFrom<IReadOnlyList<CustomerSearchSuggestion>>(json.Value);
        Assert.Equal(approved.Id, Assert.Single(suggestions).Id);
        var authorize = Assert.Single(typeof(AdminPortalAccessController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(ApplicationRoles.Administrator, authorize.Roles);
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
