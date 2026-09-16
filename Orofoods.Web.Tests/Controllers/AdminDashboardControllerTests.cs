using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Tests.Infrastructure;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Tests.Controllers;

public class AdminDashboardControllerTests
{
    [Fact]
    public async Task Index_counts_failed_integrations_and_low_stock_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99" };
        var user = new ApplicationUser { Id = "user-1", UserName = "user@test", Email = "user@test" };
        var product = new Product { Sku = "BIM-001", Name = "Pao", Brand = "Bimbo", ProductCategory = new ProductCategory { Name = "Congelados", Slug = "congelados" } };
        db.AddRange(customer, user, new Order { Customer = customer, CreatedByUser = user, Number = "ORO-1", IntegrationStatus = IntegrationStatus.Failed }, new ProductInventory { Product = product, QuantityOnHand = 10, QuantityReserved = 1 });
        await db.SaveChangesAsync();
        var recoveredOrder = new Order { Customer = customer, CreatedByUser = user, Number = "ORO-2" };
        db.Orders.Add(recoveredOrder);
        await db.SaveChangesAsync();
        db.WmcExportAudits.AddRange(
            new WmcExportAudit { OrderId = recoveredOrder.Id, Succeeded = false, Error = "Codigo WMC ausente", ExportedAt = DateTime.UtcNow.AddMinutes(-2) },
            new WmcExportAudit { OrderId = recoveredOrder.Id, Succeeded = true, FileName = "WMC_ORO2.txt", ExportedAt = DateTime.UtcNow.AddMinutes(-1) },
            new WmcExportAudit { OrderId = 1, Succeeded = false, Error = "Codigo WMC ausente", ExportedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await new DashboardController(db).Index();
        var model = Assert.IsType<AdminDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Equal(1, model.FailedIntegrations);
        Assert.Equal(1, model.LowStockProducts);
        Assert.Equal(1, model.FailedWmcExports);
    }
}
