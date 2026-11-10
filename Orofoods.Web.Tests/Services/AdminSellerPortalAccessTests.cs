using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Orofoods.Web.Identity;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class AdminSellerPortalAccessTests
{
    private const string SelectedRepresentativeClaim = AdminSellerContextService.SelectedRepresentativeClaimType;

    [Fact]
    public async Task Middleware_adds_the_selected_representative_claim_only_for_the_selecting_admin()
    {
        var session = new TestSession();
        var contextService = new AdminSellerContextService();
        contextService.SetSelectedRepresentativeId(session, "admin-a", 42);
        var context = new DefaultHttpContext
        {
            User = CreateAdministrator(null, "admin-a"),
            Session = session
        };

        await new AdminSellerContextMiddleware(_ => Task.CompletedTask).InvokeAsync(context, contextService);

        Assert.Equal("42", context.User.FindFirst(SelectedRepresentativeClaim)?.Value);
        Assert.Null(contextService.GetSelectedRepresentativeId(session, "admin-b"));
    }

    [Fact]
    public async Task Administrator_entering_seller_portal_without_selection_is_sent_to_seller_selector()
    {
        var context = new DefaultHttpContext
        {
            User = CreateAdministrator(null),
            Session = new TestSession()
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/Vendedor";
        context.Request.QueryString = new QueryString("?q=cliente");

        await new AdminSellerContextMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(context, new AdminSellerContextService());

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Contains("/Admin/Portais/SelecionarVendedor?returnUrl=%2FVendedor%3Fq%3Dcliente", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Administrator_with_selected_active_representative_can_access_that_portfolio()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var representative = new SalesRepresentative { Name = "Ana", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Cliente Ltda",
            TradeName = "Cliente",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Approved,
            IsActive = true,
            SalesRepresentative = representative
        };
        db.AddRange(representative, customer);
        await db.SaveChangesAsync();

        var administrator = CreateAdministrator(representative.Id);
        var access = new SalesRepresentativeAccessService(db);

        var selectedRepresentative = await access.GetActiveRepresentativeAsync(administrator);
        var cartScope = await access.GetSellerCartScopeAsync(administrator, customer.Id);

        Assert.Equal(representative.Id, selectedRepresentative?.Id);
        Assert.Equal(representative.Id, cartScope?.SalesRepresentativeId);
        Assert.Equal(customer.Id, cartScope?.CustomerId);
    }

    [Fact]
    public async Task Administrator_without_selected_representative_cannot_access_seller_portal()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var access = new SalesRepresentativeAccessService(db);

        Assert.False(await access.HasActiveSellerAccessAsync(CreateAdministrator(null)));
        Assert.Null(await access.GetActiveRepresentativeAsync(CreateAdministrator(null)));
    }

    private static ClaimsPrincipal CreateAdministrator(int? representativeId, string administratorUserId = "admin-user")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, administratorUserId),
            new(ClaimTypes.Role, "Administrador")
        };
        if (representativeId is not null)
        {
            claims.Add(new Claim(SelectedRepresentativeClaim, representativeId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "admin-seller-context";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
