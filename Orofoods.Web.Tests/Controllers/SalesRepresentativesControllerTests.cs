using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class SalesRepresentativesControllerTests
{
    [Fact]
    public async Task Edit_saves_wmc_code_28_and_redirects()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var controller = CreateController(db);

        var result = await controller.Edit(new SalesRepresentative
        {
            Name = "Vendedor 28",
            Email = "vendedor28@example.test",
            Phone = "11999999999",
            Region = "Campinas",
            WmcCode = " 28 ",
            IsActive = true
        }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Vendedor salvo com sucesso.", controller.TempData["SalesRepresentativeMessage"]);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(db.SalesRepresentatives.AsNoTracking());
        Assert.Equal("28", saved.WmcCode);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task Edit_returns_validation_message_when_wmc_code_is_already_used()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.SalesRepresentatives.Add(new SalesRepresentative
        {
            Name = "Vendedor existente",
            Email = "existente@example.test",
            Phone = "11888888888",
            WmcCode = "28",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Edit(new SalesRepresentative
        {
            Name = "Outro vendedor",
            Email = "outro@example.test",
            Phone = "11777777777",
            WmcCode = "28",
            IsActive = true
        }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<SalesRepresentative>(view.Model);
        Assert.Contains(controller.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage!.Contains("Código WMC", StringComparison.OrdinalIgnoreCase));
    }

    private static SalesRepresentativesController CreateController(Orofoods.Web.Data.ApplicationDbContext db)
    {
        return new SalesRepresentativesController(db, new AdminCommercialService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new EmptyTempDataProvider())
        };
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
