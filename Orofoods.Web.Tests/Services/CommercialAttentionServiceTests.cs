using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;
using System.Security.Claims;

namespace Orofoods.Web.Tests.Services;

public class CommercialAttentionServiceTests
{
    [Fact]
    public async Task GetAsync_UsesOnlyValidOrdersAndCalculatesRepurchaseWithTolerance()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer("Recompra");
        db.Add(customer);
        await db.SaveChangesAsync();
        foreach (var date in new[] { new DateTime(2026, 9, 1), new DateTime(2026, 9, 15), new DateTime(2026, 9, 29) })
        {
            db.Orders.Add(new Order { CustomerId = customer.Id, CreatedByUserId = "operator", CreatedAt = date, Status = OrderStatus.Delivered, Total = 10m });
        }
        db.Orders.Add(new Order { CustomerId = customer.Id, CreatedByUserId = "operator", CreatedAt = new DateTime(2026, 9, 30), Status = OrderStatus.Cancelled, Total = 10m });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Principal(), new DateTime(2026, 10, 19));
        var item = Assert.Single(result.Customers);

        Assert.Equal(20, item.DaysSinceLastPurchase);
        Assert.Equal(14, item.AveragePurchaseIntervalDays);
        Assert.True(item.IsRepurchaseDue);
        Assert.Equal(1, result.RepurchasesDue);
    }

    [Fact]
    public async Task GetAsync_DoesNotPredictWithFewerThanThreeValidPurchases()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = CreateCustomer("Historico curto");
        db.Add(customer);
        await db.SaveChangesAsync();
        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, CreatedByUserId = "operator", CreatedAt = new DateTime(2026, 9, 1), Status = OrderStatus.Delivered },
            new Order { CustomerId = customer.Id, CreatedByUserId = "operator", CreatedAt = new DateTime(2026, 9, 15), Status = OrderStatus.Delivered });
        await db.SaveChangesAsync();

        var item = Assert.Single((await CreateService(db).GetAsync(Principal(), new DateTime(2026, 10, 19))).Customers);

        Assert.Null(item.AveragePurchaseIntervalDays);
        Assert.False(item.IsRepurchaseDue);
        Assert.Equal(34, item.DaysSinceLastPurchase);
    }

    [Fact]
    public async Task GetAsync_SignalsFirstContactAndOverdueReturn()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var newCustomer = CreateCustomer("Novo");
        var returnCustomer = CreateCustomer("Retorno");
        db.AddRange(newCustomer, returnCustomer);
        await db.SaveChangesAsync();
        db.CommercialActivities.Add(new Orofoods.Web.Models.Commercial.CommercialActivity
        {
            CustomerId = returnCustomer.Id,
            Type = Orofoods.Web.Models.Commercial.CommercialActivityType.Return,
            Status = Orofoods.Web.Models.Commercial.CommercialActivityStatus.Scheduled,
            ScheduledAt = new DateTime(2026, 10, 18),
            Title = "Retornar contato"
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Principal(), new DateTime(2026, 10, 19));

        Assert.Equal(2, result.NewCustomers);
        Assert.Contains(result.Routine, x => x.CustomerId == newCustomer.Id && x.Kind == "REALIZAR PRIMEIRO CONTATO");
        Assert.Equal(1, result.OverdueReturns);
        Assert.Contains(result.Routine, x => x.CustomerId == returnCustomer.Id && x.Kind == "RETORNO VENCIDO");
    }

    private static CommercialAttentionService CreateService(Orofoods.Web.Data.ApplicationDbContext db) =>
        new(db, new SalesRepresentativeAccessService(db), Options.Create(new CrmOptions()), Options.Create(new PaymentEligibilityOptions()));

    private static ClaimsPrincipal Principal() => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Administrador")], "test"));

    private static Customer CreateCustomer(string name) => new()
    {
        LegalName = $"{name} Ltda",
        TradeName = name,
        Cnpj = Guid.NewGuid().ToString()[..18],
        Status = CustomerStatus.Approved,
        IsActive = true
    };
}
