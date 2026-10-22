using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Tests.Services;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OROFOODS_TEST_POSTGRES")))
        {
            Skip = "Set OROFOODS_TEST_POSTGRES to a disposable local PostgreSQL database to run this integration test.";
        }
    }
}

public sealed class WmcManualExportPostgresIntegrationTests
{
    [PostgresFact]
    public async Task Confirmation_is_atomic_idempotent_under_race_and_rolls_back_when_audit_insert_fails()
    {
        var connectionString = Environment.GetEnvironmentVariable("OROFOODS_TEST_POSTGRES")!;
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        Assert.Equal("wmc_confirmation_test", connection.Database);

        await using (var guardConnection = new NpgsqlConnection(connectionString))
        {
            await guardConnection.OpenAsync();
            await using var guardCommand = new NpgsqlCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'",
                guardConnection);
            Assert.Equal(0L, (long)(await guardCommand.ExecuteScalarAsync())!);
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        int firstOrderId;
        int secondOrderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            firstOrderId = (await AddEligibleOrderAsync(setup, "ORO-2026-009901")).Id;
            secondOrderId = (await AddEligibleOrderAsync(setup, "ORO-2026-009902")).Id;
            await CreateService(setup).ExportAsync(firstOrderId, "exporter", "exporter@test.local");
            await CreateService(setup).ExportAsync(secondOrderId, "exporter", "exporter@test.local");
        }

        var barrier = new ExportedStateReadBarrier();
        var confirmationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(barrier)
            .Options;
        await using var firstDb = new ApplicationDbContext(confirmationOptions);
        await using var secondDb = new ApplicationDbContext(confirmationOptions);
        var firstConfirmation = CreateService(firstDb).ConfirmImportedAsync(firstOrderId, "admin-1", "one@test.local");
        var secondConfirmation = CreateService(secondDb).ConfirmImportedAsync(firstOrderId, "admin-2", "two@test.local");

        var confirmations = await Task.WhenAll(firstConfirmation, secondConfirmation);

        Assert.All(confirmations, result => Assert.Equal(WmcManualExportDisposition.Imported, result.Disposition));
        await using (var verification = new ApplicationDbContext(options))
        {
            Assert.Equal(WmcOrderExportStatus.Imported,
                (await verification.WmcOrderExportStates.SingleAsync(x => x.OrderId == firstOrderId)).Status);
            Assert.Single(await verification.WmcExportAudits.Where(x =>
                x.OrderId == firstOrderId && x.Outcome == WmcExportOutcome.Imported).ToListAsync());

            await verification.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION "RejectWmcImportAuditForTest"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Outcome" = 6 THEN RAISE EXCEPTION 'intentional audit failure test'; END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            await verification.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER "RejectWmcImportAuditForTest"
                BEFORE INSERT ON "WmcExportAudits"
                FOR EACH ROW EXECUTE FUNCTION "RejectWmcImportAuditForTest"();
                """);
        }

        await using (var failingDb = new ApplicationDbContext(options))
        {
            var before = await failingDb.WmcOrderExportStates.SingleAsync(x => x.OrderId == secondOrderId);
            var versionBefore = before.Version;
            var auditFailure = await Assert.ThrowsAsync<DbUpdateException>(() =>
                CreateService(failingDb).ConfirmImportedAsync(secondOrderId, "admin-3", "three@test.local"));
            Assert.IsType<PostgresException>(auditFailure.InnerException);
        }

        await using (var verification = new ApplicationDbContext(options))
        {
            var state = await verification.WmcOrderExportStates.AsNoTracking().SingleAsync(x => x.OrderId == secondOrderId);
            Assert.Equal(WmcOrderExportStatus.Exported, state.Status);
            Assert.Null(state.ImportedAt);
            Assert.Null(state.ImportedByUserId);
            Assert.Equal(2, state.Version);
            Assert.Empty(await verification.WmcExportAudits.Where(x =>
                x.OrderId == secondOrderId && x.Outcome == WmcExportOutcome.Imported).ToListAsync());
        }
    }

    private static WmcManualExportService CreateService(ApplicationDbContext db) => new(
        db,
        new WmcOrderFileGenerator(Options.Create(new WmcOrderFileOptions { SupplierCnpj = "29475222000142" })),
        new WmcExportAuditService(db));

    private static async Task<Order> AddEligibleOrderAsync(ApplicationDbContext db, string orderNumber)
    {
        var userId = $"creator-{Guid.NewGuid():N}";
        var creator = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@test.local",
            NormalizedUserName = $"{userId}@TEST.LOCAL",
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@TEST.LOCAL",
            IsActive = true
        };
        var normalizedCustomerCnpj = CreateValidNumericCnpj($"12345678{orderNumber[^4..]}");
        var order = new Order
        {
            Number = orderNumber,
            CustomerCnpjSnapshot = normalizedCustomerCnpj,
            Status = OrderStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            Customer = new Customer
            {
                LegalName = "Local WMC Test Ltda",
                TradeName = "Local WMC Test",
                Cnpj = CnpjValidator.Format(normalizedCustomerCnpj),
                WmcCode = "107072"
            },
            CreatedByUser = creator,
            PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 },
            Total = 63.20m,
            Subtotal = 63.20m,
            Items =
            [
                new OrderItem
                {
                    Product = new Product
                    {
                        Name = "Local WMC Test Product",
                        Sku = $"SKU-{orderNumber[^4..]}",
                        WmcCode = "610601552",
                        Unit = "UN",
                        ProductCategory = new ProductCategory { Name = "Local WMC Test", Slug = $"wmc-{orderNumber[^4..]}" }
                    },
                    ProductNameSnapshot = "Local WMC Test Product",
                    SkuSnapshot = "SKU-TEST",
                    Quantity = 1,
                    UnitPrice = 63.20m,
                    Subtotal = 63.20m
                }
            ]
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static string CreateValidNumericCnpj(string baseDigits)
    {
        static int Calculate(ReadOnlySpan<char> value)
        {
            var sum = 0;
            var weight = 2;
            for (var index = value.Length - 1; index >= 0; index--)
            {
                sum += (value[index] - '0') * weight;
                weight = weight == 9 ? 2 : weight + 1;
            }

            var remainder = sum % 11;
            return remainder is 0 or 1 ? 0 : 11 - remainder;
        }

        var firstDigit = Calculate(baseDigits);
        return baseDigits + firstDigit + Calculate(baseDigits + firstDigit);
    }

    private sealed class ExportedStateReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource bothReads = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int waitingReads;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"WmcOrderExportStates\"", StringComparison.Ordinal)
                && Interlocked.Increment(ref waitingReads) <= 2)
            {
                if (Volatile.Read(ref waitingReads) == 2)
                {
                    bothReads.TrySetResult();
                }

                await bothReads.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }
}
