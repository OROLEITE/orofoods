using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Tests.Services;

public sealed class SellerPostgresFactAttribute : FactAttribute
{
    public SellerPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OROFOODS_TEST_SELLER_POSTGRES")))
        {
            Skip = "Set OROFOODS_TEST_SELLER_POSTGRES to an isolated disposable PostgreSQL database to run seller submission concurrency tests.";
        }
    }
}

public sealed class SellerOrderDraftSubmissionPostgresIntegrationTests
{
    [SellerPostgresFact]
    public async Task Concurrent_submissions_for_one_draft_create_one_order_history_and_inventory_reservation()
    {
        var connectionString = Environment.GetEnvironmentVariable("OROFOODS_TEST_SELLER_POSTGRES")!;
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        Assert.Equal("seller_submission_test", connection.Database);

        await using (var guardConnection = new NpgsqlConnection(connectionString))
        {
            await guardConnection.OpenAsync();
            await using var guardCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'",
                guardConnection);
            Assert.Equal(0L, (long)(await guardCommand.ExecuteScalarAsync())!);
        }

        var barrier = new DraftReadBarrier();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var initialContext = CreateContext(options);
        var (sellerId, draftId, expectedVersion, productId) = await SeedAsync(initialContext);
        var quoteProtector = new SellerOrderDraftQuoteProtector(new EphemeralDataProtectionProvider());
        var seededDraft = await initialContext.SellerOrderDrafts.Include(x => x.Items).SingleAsync(x => x.Id == draftId);
        var prices = await new PriceService(initialContext).GetPricesAsync(
            seededDraft.CustomerId, seededDraft.Items.Select(item => item.ProductId).ToArray());
        var quoteLines = seededDraft.Items.OrderBy(item => item.ProductId).Select(item =>
        {
            var unitPrice = prices.GetValueOrDefault(item.ProductId, item.Product?.PromotionalPrice ?? item.Product?.BasePrice ?? 0m);
            return new OrderPlacementQuoteLine(item.ProductId, item.Quantity, unitPrice, item.RequestedDiscountAmount, unitPrice * item.Quantity);
        }).ToArray();
        var subtotal = quoteLines.Sum(item => item.Subtotal);
        var quoteToken = quoteProtector.Protect(sellerId, draftId, expectedVersion, new OrderPlacementQuote(
            seededDraft.CustomerId, seededDraft.DeliveryAddressId, seededDraft.PaymentTermId, seededDraft.RequestedDeliveryDate,
            quoteLines, subtotal, 0m, subtotal));

        var concurrentOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(barrier)
            .Options;
        await using var firstDb = CreateContext(concurrentOptions);
        await using var secondDb = CreateContext(concurrentOptions);
        var seller = SellerPrincipal(sellerId);
        var first = CreateService(firstDb, quoteProtector).SubmitAsync(seller, draftId, expectedVersion, quoteToken);
        var second = CreateService(secondDb, quoteProtector).SubmitAsync(seller, draftId, expectedVersion, quoteToken);

        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorMessage));
        Assert.Single(results.Select(result => result.OrderId).Distinct());
        Assert.Contains(results, result => result.WasIdempotentReplay);
        await using var verify = CreateContext(options);
        var order = await verify.Orders.Include(x => x.StatusHistory).SingleAsync(x => x.SellerOrderDraftId == draftId);
        Assert.Equal(OrderStatus.Received, order.Status);
        Assert.Single(order.StatusHistory);
        Assert.Single(await verify.InventoryReservations.Where(x => x.OrderId == order.Id && x.Status == InventoryReservationStatus.Active).ToListAsync());
        Assert.Equal(2, await verify.ProductInventories.Where(x => x.ProductId == productId)
            .Select(x => x.QuantityReserved).SingleAsync());
        var submitted = await verify.SellerOrderDrafts.AsNoTracking().SingleAsync(x => x.Id == draftId);
        Assert.Equal(SellerOrderDraftStatus.Submitted, submitted.Status);
        Assert.Equal(order.Id, await verify.Orders.Where(x => x.SellerOrderDraftId == submitted.Id)
            .Select(x => x.Id).SingleAsync());
    }

    private static ApplicationDbContext CreateContext(DbContextOptions<ApplicationDbContext> options) => new(options);

    private static ClaimsPrincipal SellerPrincipal(string sellerId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, sellerId), new Claim(ClaimTypes.Role, "Vendedor")], "test"));

    private static SellerOrderDraftSubmissionService CreateService(ApplicationDbContext db, SellerOrderDraftQuoteProtector quoteProtector)
    {
        var access = new SalesRepresentativeAccessService(db);
        var prices = new PriceService(db);
        var eligibility = new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions()));
        return new SellerOrderDraftSubmissionService(db, access,
            new OrderPlacementService(db, new CartService(db, prices), prices, eligibility, new OrderReservationService(db)),
            quoteProtector,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SellerOrderDraftSubmissionService>.Instance);
    }

    private static async Task<(string SellerId, int DraftId, int ExpectedVersion, int ProductId)> SeedAsync(ApplicationDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        var representative = new SalesRepresentative { Name = "Vendedor PG", IsActive = true };
        var seller = new ApplicationUser { Id = "seller-pg-concurrency", UserName = "seller-pg-concurrency", IsActive = true, SalesRepresentative = representative };
        var priceTable = new PriceTable { Name = "Tabela PG", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Cliente PG",
            TradeName = "Cliente PG",
            Cnpj = "12345678000195",
            Status = CustomerStatus.Approved,
            IsActive = true,
            MinimumOrder = 1m,
            CreditLimit = 1000m,
            SalesRepresentative = representative,
            PriceTable = priceTable
        };
        var address = new CustomerAddress
        {
            Customer = customer,
            Label = "Endereço principal",
            Street = "Rua PG",
            Number = "10",
            City = "Campinas",
            State = "SP",
            IsActive = true
        };
        var paymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0, IsActive = true };
        var product = new Product
        {
            Sku = "SELLER-PG-001",
            Name = "Produto PG",
            Brand = "Orofoods",
            Unit = "caixa",
            BasePrice = 50m,
            MinimumCases = 1,
            IsActive = true,
            IsAvailable = true,
            ProductCategory = new ProductCategory { Name = "Categoria PG", Slug = "seller-pg" }
        };
        db.AddRange(representative, seller, priceTable, customer, address, paymentTerm, product,
            new PriceTableItem { PriceTable = priceTable, Product = product, Price = 37m },
            new ProductInventory { Product = product, QuantityOnHand = 10 });
        await db.SaveChangesAsync();

        var draft = new SellerOrderDraft
        {
            OwnerUserId = seller.Id,
            SalesRepresentativeId = representative.Id,
            CustomerId = customer.Id,
            DeliveryAddressId = address.Id,
            PaymentTermId = paymentTerm.Id,
            Notes = "Pedido concorrente",
            Status = SellerOrderDraftStatus.Draft,
            Version = 1,
            Items = [new SellerOrderDraftItem { ProductId = product.Id, Quantity = 2, RequestedDiscountAmount = 0m }]
        };
        db.SellerOrderDrafts.Add(draft);
        await db.SaveChangesAsync();
        return (seller.Id, draft.Id, draft.Version, product.Id);
    }

    private sealed class DraftReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothReadsAreReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _waitingReads;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"SellerOrderDrafts\"", StringComparison.Ordinal)
                && Interlocked.Increment(ref _waitingReads) <= 2)
            {
                if (Volatile.Read(ref _waitingReads) == 2)
                {
                    _bothReadsAreReady.TrySetResult();
                }

                await _bothReadsAreReady.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }

            return result;
        }
    }
}
