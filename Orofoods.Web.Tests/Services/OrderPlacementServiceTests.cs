using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class OrderPlacementServiceTests
{
    [Fact]
    public async Task PlaceAsync_creates_a_received_order_with_current_price_and_history()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), "Observacao"),
            fixture.Session,
            fixture.Scope);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var order = result.Order!;
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Equal(fixture.Customer.Cnpj, order.CustomerCnpjSnapshot);
        Assert.Equal(fixture.SellerUser.Id, order.CreatedByUserId);
        Assert.Equal(fixture.Customer.SalesRepresentativeId, order.SalesRepresentativeId);
        Assert.Equal(fixture.CurrentPrice, Assert.Single(order.Items).UnitPrice);
        Assert.Equal(OrderStatus.Received, Assert.Single(order.StatusHistory).Status);
        Assert.False(fixture.Session.TryGetValue("orofoods-cart-v2-product-ids:seller:7:customer:1", out _));
    }

    [Fact]
    public async Task PlaceLinesAsync_rejects_provisional_Bimbo_import_price()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Product.Brand = "BIMBO";
        fixture.Product.WmcCode = "503768";
        fixture.Product.WmcBrandCode = "113";
        fixture.Product.IsWmcActive = true;
        fixture.Product.WmcStockAvailable = true;
        fixture.Product.WmcInitialLoadReady = true;
        fixture.Product.BasePrice = 1m;
        fixture.Product.PromotionalPrice = null;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, null, null),
            [(fixture.Product.Id, 1)],
            clearCart: false);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Confirmed_order_keeps_customer_cnpj_snapshot_after_customer_correction()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var originalCnpj = fixture.Customer.Cnpj;
        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            [(fixture.Product.Id, 1)],
            clearCart: false);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        fixture.Customer.Cnpj = "00.000.000/E08G-12";
        await fixture.Db.SaveChangesAsync();

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(order => order.Id == result.Order!.Id);
        Assert.Equal(originalCnpj, persistedOrder.CustomerCnpjSnapshot);
    }

    [Fact]
    public async Task PlaceLinesAsync_rejects_subtotal_below_minimum_even_when_customer_minimum_is_lower()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10, minimumOrderSetting: null);

        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, null, null),
            [(fixture.Product.Id, 1)],
            clearCart: false);

        Assert.False(result.Succeeded);
        var minimumError = Assert.Single(result.Errors);
        Assert.Contains("valor mínimo dos itens", minimumError, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"\b160\b", minimumError);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Package_sales_accepts_whole_pack_quantities_without_case_multiples_and_snapshots_the_unit()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Product.CommercialUnitCode = "PCT";
        fixture.Product.LogisticsUnitCode = "CX";
        fixture.Product.PackagesPerCase = 12;
        fixture.Product.MinimumCases = 12;
        fixture.Product.BasePrice = 99m;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, null, null),
            [(fixture.Product.Id, 3)],
            clearCart: false);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var item = Assert.Single(result.Order!.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal("PCT", item.SalesUnitCodeSnapshot);
        Assert.Equal(297m, item.Subtotal);
    }

    [Fact]
    public async Task PlaceLinesAsync_accepts_the_configured_minimum_exactly()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10, minimumOrderSetting: 160m);
        fixture.Product.BasePrice = 80m;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, null, null),
            [(fixture.Product.Id, 2)],
            clearCart: false);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(160m, result.Order!.Subtotal);
    }

    [Fact]
    public async Task PlaceLinesAsync_snapshots_the_customer_external_seller_separately_from_the_creator()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var internalOperator = new ApplicationUser { Id = "internal-operator", UserName = "internal-operator", Email = "operator@test.local", IsActive = true };
        fixture.Db.Users.Add(internalOperator);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            internalOperator.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            [(fixture.Product.Id, 1)],
            clearCart: false);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var order = result.Order!;
        Assert.Equal(fixture.Customer.SalesRepresentativeId, order.SalesRepresentativeId);
        Assert.Equal(internalOperator.Id, order.CreatedByUserId);

        var originalRepresentativeId = fixture.Customer.SalesRepresentativeId;
        var replacementRepresentative = new SalesRepresentative { Name = "Novo vendedor", IsActive = true };
        fixture.Db.SalesRepresentatives.Add(replacementRepresentative);
        await fixture.Db.SaveChangesAsync();
        fixture.Customer.SalesRepresentative = replacementRepresentative;
        fixture.Customer.SalesRepresentativeId = replacementRepresentative.Id;
        await fixture.Db.SaveChangesAsync();

        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(row => row.Id == order.Id);
        Assert.NotEqual(originalRepresentativeId, fixture.Customer.SalesRepresentativeId);
        Assert.Equal(originalRepresentativeId, persistedOrder.SalesRepresentativeId);
    }

    [Fact]
    public async Task PlaceLinesAsync_keeps_sales_representative_snapshot_null_when_customer_has_no_external_seller()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Customer.SalesRepresentativeId = null;
        fixture.Customer.SalesRepresentative = null;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceLinesAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            [(fixture.Product.Id, 1)],
            clearCart: false);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var persistedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(order => order.Id == result.Order!.Id);
        Assert.Null(persistedOrder.SalesRepresentativeId);
    }

    [Fact]
    public async Task PlaceDraftLinesWithinTransactionAsync_creates_order_and_reservations_without_committing_callers_transaction()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var draft = await fixture.CreateDraftAsync();
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync();
        var command = new OrderPlacementCommand(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), "Rascunho enviado");
        var lines = new[] { (fixture.Product.Id, 2) };
        var quote = await fixture.CreateQuoteAsync(command, lines);

        var result = await fixture.Service.PlaceDraftLinesWithinTransactionAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            command,
            draft.Id,
            lines,
            quote);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(draft.Id, result.Order!.SellerOrderDraftId);
        Assert.Equal(2, Assert.Single(result.Order.Items).Quantity);
        Assert.Single(await fixture.Db.InventoryReservations.Where(x => x.OrderId == result.Order.Id).ToListAsync());
        Assert.NotNull(fixture.Db.Database.CurrentTransaction);

        await transaction.RollbackAsync();

        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.AsNoTracking().ToListAsync());
        Assert.Equal(0, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
    }

    [Fact]
    public async Task PlaceDraftLinesWithinTransactionAsync_rejects_the_whole_set_when_one_quantity_is_invalid()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var secondProduct = await fixture.AddAvailableProductAsync("PLACE-002");
        var draft = await fixture.CreateDraftAsync();
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync();
        var command = new OrderPlacementCommand(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null);
        var lines = new[] { (fixture.Product.Id, 1), (secondProduct.Id, 0) };
        var quote = await fixture.CreateQuoteAsync(command, lines);

        var result = await fixture.Service.PlaceDraftLinesWithinTransactionAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            command,
            draft.Id,
            lines,
            quote);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PlaceDraftLinesWithinTransactionAsync_rejects_duplicate_lines_instead_of_merging_them()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var draft = await fixture.CreateDraftAsync();
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync();
        var command = new OrderPlacementCommand(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null);
        var lines = new[] { (fixture.Product.Id, 1), (fixture.Product.Id, 2) };
        var quote = await fixture.CreateQuoteAsync(command, lines);

        var result = await fixture.Service.PlaceDraftLinesWithinTransactionAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            command,
            draft.Id,
            lines,
            quote);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PlaceDraftLinesWithinTransactionAsync_rejects_below_minimum_instead_of_raising_quantity()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Product.MinimumCases = 3;
        await fixture.Db.SaveChangesAsync();
        var draft = await fixture.CreateDraftAsync();
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync();
        var command = new OrderPlacementCommand(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null);
        var lines = new[] { (fixture.Product.Id, 1) };
        var quote = await fixture.CreateQuoteAsync(command, lines);

        var result = await fixture.Service.PlaceDraftLinesWithinTransactionAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            command,
            draft.Id,
            lines,
            quote);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.AsNoTracking().ToListAsync());
        Assert.Equal(0, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id)
            .Select(x => x.QuantityReserved).SingleAsync());
    }

    [Fact]
    public async Task Card_on_delivery_creates_a_local_pending_payment_without_gateway_metadata()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10, paymentTermCode: "CARD_ON_DELIVERY");

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), "Pagamento na entrega"),
            fixture.Session,
            fixture.Scope);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var createdOrder = result.Order!;
        var payment = await fixture.Db.Payments.SingleAsync(x => x.OrderId == createdOrder.Id);
        Assert.Equal(PaymentMethodType.CardOnDelivery, payment.Method);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(createdOrder.Total, payment.Amount);
        Assert.Equal("CARD_ON_DELIVERY", payment.PaymentMethod);
        Assert.Null(payment.Gateway);
        Assert.Null(payment.GatewayOrderId);
        Assert.Null(payment.GatewayPaymentId);
        Assert.Null(payment.ExternalPaymentId);
        Assert.Null(payment.IdempotencyKey);
    }

    [Fact]
    public async Task PlaceLinesAsync_returns_existing_order_for_the_same_customer_and_attempt_key()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var command = new OrderPlacementCommand(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null, "same-attempt");

        var first = await fixture.Service.PlaceLinesAsync(fixture.Customer.Id, fixture.SellerUser.Id, command, [(fixture.Product.Id, 1)], clearCart: false);
        var replay = await fixture.Service.PlaceLinesAsync(fixture.Customer.Id, fixture.SellerUser.Id, command, [(fixture.Product.Id, 1)], clearCart: false);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));
        Assert.True(replay.Succeeded, string.Join("; ", replay.Errors));
        Assert.True(replay.WasIdempotentReplay);
        Assert.Equal(first.Order!.Id, replay.Order!.Id);
        Assert.Equal("same-attempt", replay.Order.CheckoutAttemptKey);
        Assert.Equal(1, await fixture.Db.Orders.CountAsync(x => x.CustomerId == fixture.Customer.Id));
        Assert.Equal(1, await fixture.Db.InventoryReservations.CountAsync(x => x.Status == InventoryReservationStatus.Active));
        Assert.Equal(1, await fixture.Db.ProductInventories.Where(x => x.ProductId == fixture.Product.Id).Select(x => x.QuantityReserved).SingleAsync());
    }

    [Fact]
    public async Task PlaceLinesAsync_allows_a_new_order_for_a_different_attempt_key()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);

        var first = await fixture.Service.PlaceLinesAsync(fixture.Customer.Id, fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null, "attempt-one"), [(fixture.Product.Id, 1)], clearCart: false);
        var second = await fixture.Service.PlaceLinesAsync(fixture.Customer.Id, fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null, "attempt-two"), [(fixture.Product.Id, 1)], clearCart: false);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));
        Assert.True(second.Succeeded, string.Join("; ", second.Errors));
        Assert.NotEqual(first.Order!.Id, second.Order!.Id);
        Assert.Equal(2, await fixture.Db.Orders.CountAsync(x => x.CustomerId == fixture.Customer.Id));
        Assert.Equal(2, await fixture.Db.InventoryReservations.CountAsync(x => x.Status == InventoryReservationStatus.Active));
    }

    [Fact]
    public async Task PlaceLinesAsync_allows_the_same_attempt_key_for_a_different_customer()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        var first = await fixture.Service.PlaceLinesAsync(fixture.Customer.Id, fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null, "shared-attempt"), [(fixture.Product.Id, 1)], clearCart: false);
        var otherCustomer = new Customer
        {
            LegalName = "Outro cliente", TradeName = "Outro cliente", Cnpj = "22.222.222/0001-22",
            Status = CustomerStatus.Approved, IsActive = true, MinimumOrder = 1m,
            Addresses = [new CustomerAddress { Label = "Principal", Street = "Rua B", Number = "2", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true }]
        };
        fixture.Db.Customers.Add(otherCustomer);
        await fixture.Db.SaveChangesAsync();
        var otherAddress = Assert.Single(otherCustomer.Addresses);

        var second = await fixture.Service.PlaceLinesAsync(otherCustomer.Id, fixture.SellerUser.Id,
            new(otherAddress.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null, "shared-attempt"), [(fixture.Product.Id, 1)], clearCart: false);

        Assert.True(first.Succeeded, string.Join("; ", first.Errors));
        Assert.True(second.Succeeded, string.Join("; ", second.Errors));
        Assert.NotEqual(first.Order!.Id, second.Order!.Id);
        Assert.Equal(1, await fixture.Db.Orders.CountAsync(x => x.CheckoutAttemptKey == "shared-attempt" && x.CustomerId == fixture.Customer.Id));
        Assert.Equal(1, await fixture.Db.Orders.CountAsync(x => x.CheckoutAttemptKey == "shared-attempt" && x.CustomerId == otherCustomer.Id));
        Assert.Equal(2, await fixture.Db.InventoryReservations.CountAsync(x => x.Status == InventoryReservationStatus.Active));
    }

    [Fact]
    public async Task Orders_reject_duplicate_customer_attempt_keys_at_the_database_level()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Db.Orders.AddRange(
            new Order { Number = "ORDER-UNIQUE-1", CustomerId = fixture.Customer.Id, CreatedByUserId = fixture.SellerUser.Id, PaymentTermId = fixture.PaymentTerm.Id, CheckoutAttemptKey = "database-unique", Status = OrderStatus.Received },
            new Order { Number = "ORDER-UNIQUE-2", CustomerId = fixture.Customer.Id, CreatedByUserId = fixture.SellerUser.Id, PaymentTermId = fixture.PaymentTerm.Id, CheckoutAttemptKey = "database-unique", Status = OrderStatus.Received });

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task PlaceAsync_rejects_a_scope_for_a_different_customer_without_persisting()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            fixture.Session,
            CartScope.ForSeller(7, fixture.Customer.Id + 1));

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task PlaceAsync_rolls_back_when_inventory_reservation_fails()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 0);
        fixture.Session.SetString("orofoods-cart-v2-product-ids:seller:7:customer:1", JsonSerializer.Serialize(new Dictionary<int, int> { [fixture.Product.Id] = 1 }));

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            fixture.Session,
            fixture.Scope);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
        Assert.Empty(await fixture.Db.InventoryReservations.ToListAsync());
    }

    [Fact]
    public async Task PlaceAsync_rejects_an_unapproved_customer()
    {
        await using var fixture = await PlacementFixture.CreateAsync(availableQuantity: 10);
        fixture.Customer.Status = CustomerStatus.Pending;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.PlaceAsync(
            fixture.Customer.Id,
            fixture.SellerUser.Id,
            new(fixture.Address.Id, fixture.PaymentTerm.Id, DateTime.UtcNow.AddDays(1), null),
            fixture.Session,
            fixture.Scope);

        Assert.False(result.Succeeded);
        Assert.Empty(await fixture.Db.Orders.ToListAsync());
    }

    private sealed class PlacementFixture : IAsyncDisposable
    {
        public required Orofoods.Web.Data.ApplicationDbContext Db { get; init; }
        public required OrderPlacementService Service { get; init; }
        public required Customer Customer { get; init; }
        public required ApplicationUser SellerUser { get; init; }
        public required CustomerAddress Address { get; init; }
        public required PaymentTerm PaymentTerm { get; init; }
        public required Product Product { get; init; }
        public required decimal CurrentPrice { get; init; }
        public required TestSession Session { get; init; }
        public required CartScope Scope { get; init; }

        public async Task<SellerOrderDraft> CreateDraftAsync()
        {
            var draft = new SellerOrderDraft
            {
                OwnerUserId = SellerUser.Id,
                SalesRepresentativeId = Customer.SalesRepresentativeId!.Value,
                CustomerId = Customer.Id,
                DeliveryAddressId = Address.Id,
                PaymentTermId = PaymentTerm.Id,
                Status = SellerOrderDraftStatus.Draft,
                Version = 1
            };
            Db.SellerOrderDrafts.Add(draft);
            await Db.SaveChangesAsync();
            return draft;
        }

        public async Task<OrderPlacementQuote> CreateQuoteAsync(
            OrderPlacementCommand command,
            IReadOnlyList<(int ProductId, int Quantity)> lines)
        {
            var prices = await new Orofoods.Web.Services.Pricing.PriceService(Db)
                .GetPricesAsync(Customer.Id, lines.Select(line => line.ProductId).Distinct().ToArray());
            var quotedLines = lines.Select(line =>
            {
                var unitPrice = prices.GetValueOrDefault(line.ProductId, Product.BasePrice);
                return new OrderPlacementQuoteLine(line.ProductId, line.Quantity, unitPrice, 0m, unitPrice * line.Quantity);
            }).ToArray();
            var subtotal = quotedLines.Sum(line => line.Subtotal);
            return new OrderPlacementQuote(
                Customer.Id,
                command.AddressId,
                command.PaymentTermId,
                command.RequestedDeliveryDate,
                quotedLines,
                subtotal,
                0m,
                subtotal);
        }

        public async Task<Product> AddAvailableProductAsync(string sku)
        {
            var product = new Product
            {
                Sku = sku,
                Name = $"Produto {sku}",
                Brand = "Orofoods",
                Unit = "caixa",
                BasePrice = 50m,
                MinimumCases = 1,
                IsActive = true,
                IsAvailable = true,
                ProductCategory = new ProductCategory { Name = $"Categoria {sku}", Slug = sku.ToLowerInvariant() }
            };
            Db.AddRange(product, new ProductInventory { Product = product, QuantityOnHand = 10 });
            await Db.SaveChangesAsync();
            return product;
        }

        public static async Task<PlacementFixture> CreateAsync(int availableQuantity, string paymentTermCode = "PIX", decimal? minimumOrderSetting = 1m)
        {
            var db = await TestDbContextFactory.CreateAsync();
            var representative = new SalesRepresentative { Id = 7, Name = "Vendedor", IsActive = true };
            var customer = new Customer { Id = 1, LegalName = "Cliente", TradeName = "Cliente", Cnpj = "11.111.111/0001-11", Status = CustomerStatus.Approved, IsActive = true, MinimumOrder = 1m, CreditLimit = 1000m, SalesRepresentative = representative };
            var address = new CustomerAddress { Label = "Principal", Street = "Rua A", Number = "1", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", IsActive = true, IsPrimary = true, Customer = customer };
            var term = new PaymentTerm { Code = paymentTermCode, Name = paymentTermCode == "CARD_ON_DELIVERY" ? "Cartão na entrega" : "PIX", DaysUntilDue = 0, IsActive = true };
            var product = new Product { Sku = "PLACE-001", Name = "Produto", Brand = "Orofoods", Unit = "caixa", BasePrice = 99m, MinimumCases = 1, IsActive = true, IsAvailable = true, ProductCategory = new ProductCategory { Name = "Categoria", Slug = "categoria" } };
            var seller = new ApplicationUser { Id = "seller", UserName = "seller", Email = "seller@test.local", IsActive = true, SalesRepresentative = representative };
            db.AddRange(representative, customer, address, term, product, seller, new ProductInventory { Product = product, QuantityOnHand = availableQuantity });
            if (minimumOrderSetting.HasValue) db.OrderMinimumSettings.Add(new OrderMinimumSettings { MinimumAmount = minimumOrderSetting.Value });
            await db.SaveChangesAsync();

            var session = new TestSession();
            var scope = CartScope.ForSeller(representative.Id, customer.Id);
            session.SetString("orofoods-cart-v2-product-ids:seller:7:customer:1", JsonSerializer.Serialize(new Dictionary<int, int> { [product.Id] = 1 }));
            return new PlacementFixture
            {
                Db = db,
                Service = new OrderPlacementService(db, new CartService(db, new Orofoods.Web.Services.Pricing.PriceService(db)), new Orofoods.Web.Services.Pricing.PriceService(db), new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions { CardOnDeliveryEnabled = paymentTermCode == "CARD_ON_DELIVERY" })), new OrderReservationService(db)),
                Customer = customer,
                SellerUser = seller,
                Address = address,
                PaymentTerm = term,
                Product = product,
                CurrentPrice = product.BasePrice,
                Session = session,
                Scope = scope
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public IEnumerable<string> Keys => values.Keys;
        public string Id => "placement-test";
        public bool IsAvailable => true;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }
}
