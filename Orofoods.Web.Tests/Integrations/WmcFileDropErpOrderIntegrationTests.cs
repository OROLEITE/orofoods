using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Integrations;

public class WmcFileDropErpOrderIntegrationTests
{
    [Fact]
    public void Does_not_enable_automatic_retry_by_default()
    {
        var options = new WmcFileDropOptions();

        Assert.False(options.AutoRetryEnabled);
    }

    [Fact]
    public async Task Writes_a_mapped_order_to_the_configured_directory_when_enabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = directory });
        var order = new Order
        {
            Number = "ORO-2026-000777",
            Status = OrderStatus.Approved,
            Customer = new Customer { WmcCode = "107072" },
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "610601552", Unit = "caixa" },
                    ProductNameSnapshot = "Pao brioche",
                    Quantity = 2,
                    UnitPrice = 50m,
                    Subtotal = 100m
                }
            ]
        };
        await using var db = await TestDbContextFactory.CreateAsync();
        await PersistOrderAsync(db, order);
        Assert.True(order.Id > 0 && await db.Orders.AnyAsync(existing => existing.Id == order.Id), $"Order id after test seed: {order.Id}");

        var result = await CreateAdapter(options, db).SendOrderAsync(order);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ExternalOrderId);
        Assert.True(File.Exists(Path.Combine(directory, result.ExternalOrderId!)));
    }

    [Fact]
    public async Task Does_not_write_files_when_the_adapter_is_disabled()
    {
        var options = Options.Create(new WmcFileDropOptions { Enabled = false });
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = ApprovedOrder("ORO-2026-000781");
        await PersistOrderAsync(db, order);

        var result = await CreateAdapter(options, db).SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Contains("desativada", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reprocessing_the_same_order_preserves_the_existing_wmc_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = directory });
        var order = new Order
        {
            Number = "ORO-2026-000778",
            Status = OrderStatus.Approved,
            Customer = new Customer { WmcCode = "107072" },
            Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }]
        };
        await using var db = await TestDbContextFactory.CreateAsync();
        await PersistOrderAsync(db, order);
        var sut = CreateAdapter(options, db);

        var first = await sut.SendOrderAsync(order);
        var path = Path.Combine(directory, first.ExternalOrderId!);
        var originalContent = await File.ReadAllTextAsync(path);
        var second = await sut.SendOrderAsync(order);

        Assert.True(second.Succeeded);
        Assert.Equal(first.ExternalOrderId, second.ExternalOrderId);
        Assert.Equal(originalContent, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Existing_file_with_content_that_does_not_match_the_order_is_not_reported_as_success()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = directory });
        var order = new Order
        {
            Number = "ORO-2026-000780",
            CreatedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc),
            Status = OrderStatus.Approved,
            Customer = new Customer { WmcCode = "107072" },
            Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }]
        };
        await using var db = await TestDbContextFactory.CreateAsync();
        await PersistOrderAsync(db, order);
        var sut = CreateAdapter(options, db);
        var first = await sut.SendOrderAsync(order);
        var path = Path.Combine(directory, first.ExternalOrderId!);
        await File.WriteAllTextAsync(path, "arquivo de outro conteúdo/pedido");

        var retry = await sut.SendOrderAsync(order);

        Assert.False(retry.Succeeded);
        Assert.Contains("conteúdo", retry.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("arquivo de outro conteúdo/pedido", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Card_on_delivery_is_blocked_before_file_write_with_explicit_mapping_marker()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = directory });
        var order = new Order
        {
            Number = "ORO-2026-000779",
            Status = OrderStatus.Approved,
            Customer = new Customer { WmcCode = "107072" },
            PaymentTerm = new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega" },
            Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }]
        };
        await using var db = await TestDbContextFactory.CreateAsync();
        await PersistOrderAsync(db, order);

        var result = await CreateAdapter(options, db).SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Contains("WMC_CARD_ON_DELIVERY_MAPPING_PENDING", result.Error);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task Does_not_export_or_audit_an_unapproved_order()
    {
        var options = Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}") });
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = ApprovedOrder("ORO-2026-000782");
        order.Status = OrderStatus.UnderReview;
        await PersistOrderAsync(db, order);

        var result = await CreateAdapter(options, db).SendOrderAsync(order);

        Assert.False(result.Succeeded);
        Assert.Empty(db.WmcExportAudits);
    }

    private static Order ApprovedOrder(string number) => new()
    {
        Number = number,
        Status = OrderStatus.Approved,
        Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" },
        Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 50m, Subtotal = 50m }]
    };

    private static async Task PersistOrderAsync(ApplicationDbContext db, Order order)
    {
        order.Customer ??= new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" };
        order.CreatedByUser = TestDbContextFactory.CreateOrderCreator($"wmc-{Guid.NewGuid():N}");
        var items = order.Items.ToList();
        order.Items.Clear();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        db.Entry(order).State = EntityState.Detached;
        foreach (var item in items)
        {
            order.Items.Add(item);
        }
    }

    private static WmcFileDropErpOrderIntegration CreateAdapter(IOptions<WmcFileDropOptions> options, ApplicationDbContext db) =>
        new(options, new WmcOrderFileGenerator(), new WmcExportAuditService(db));
}
