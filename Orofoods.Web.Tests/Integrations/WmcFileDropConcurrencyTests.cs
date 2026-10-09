using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Integrations;

public class WmcFileDropConcurrencyTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task Concurrent_attempts_for_the_same_order_keep_file_and_audits_consistent(
        bool sameAttemptId,
        int expectedAuditCount)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}.db");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"orofoods-wmc-{Guid.NewGuid():N}");
        var interceptor = new ConcurrentAuditReadBarrier();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString())
            .AddInterceptors(interceptor)
            .Options;

        try
        {
            Order order;
            await using (var seedDb = new ApplicationDbContext(options))
            {
                await seedDb.Database.EnsureCreatedAsync();
                order = new Order
                {
                    Number = "ORO-2026-009901",
                    Status = OrderStatus.Approved,
                    Customer = new Customer { LegalName = "Cliente de teste", TradeName = "Teste", Cnpj = "12345678000199", WmcCode = "107072" },
                    CreatedByUser = TestDbContextFactory.CreateOrderCreator("wmc-concurrency-test"),
                    Items =
                    [
                        new OrderItem
                        {
                            Product = new Product
                            {
                                Sku = "WMC-TEST",
                                WmcCode = "610601552",
                                Name = "Produto de teste",
                                Unit = "caixa",
                                ProductCategory = new ProductCategory { Name = "Categoria de teste", Slug = "categoria-teste", IsActive = true }
                            },
                            ProductNameSnapshot = "Produto de teste",
                            Quantity = 1,
                            UnitPrice = 12m,
                            Subtotal = 12m
                        }
                    ]
                };
                seedDb.Orders.Add(order);
                await seedDb.SaveChangesAsync();
            }

            var firstAttemptId = Guid.NewGuid();
            var secondAttemptId = sameAttemptId ? firstAttemptId : Guid.NewGuid();
            await using var firstDb = new ApplicationDbContext(options);
            await using var secondDb = new ApplicationDbContext(options);
            var firstAdapter = CreateAdapter(firstDb, outputDirectory);
            var secondAdapter = CreateAdapter(secondDb, outputDirectory);

            var results = await Task.WhenAll(
                firstAdapter.SendOrderAsync(order, attemptId: firstAttemptId),
                secondAdapter.SendOrderAsync(order, attemptId: secondAttemptId));

            Assert.All(results, result => Assert.True(result.Succeeded, result.Error));
            Assert.Equal(results[0].ExternalOrderId, results[1].ExternalOrderId);
            Assert.Single(Directory.GetFiles(outputDirectory, "*.txt"));

            await using var verifyDb = new ApplicationDbContext(options);
            var audits = await verifyDb.WmcExportAudits.ToListAsync();
            Assert.Equal(expectedAuditCount, audits.Count);
            Assert.All(audits, audit =>
            {
                Assert.Equal(order.Id, audit.OrderId);
                Assert.Equal(Orofoods.Web.Models.Integrations.WmcExportSource.Automatic, audit.Source);
                Assert.Equal(Orofoods.Web.Models.Integrations.WmcExportOutcome.Available, audit.Outcome);
                Assert.NotNull(audit.GeneratedAt);
            });
            Assert.Contains(audits, audit => audit.AttemptId == firstAttemptId);
            if (!sameAttemptId)
            {
                Assert.Contains(audits, audit => audit.AttemptId == secondAttemptId);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, recursive: true);
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static WmcFileDropErpOrderIntegration CreateAdapter(ApplicationDbContext db, string outputDirectory) =>
        new(Options.Create(new WmcFileDropOptions { Enabled = true, OutputDirectory = outputDirectory }),
            new WmcOrderFileGenerator(), new WmcExportAuditService(db));

    private sealed class ConcurrentAuditReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothReadsFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _auditReads;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("WmcExportAudits", StringComparison.Ordinal)
                && command.CommandText.Contains("AttemptId", StringComparison.Ordinal))
            {
                var readNumber = Interlocked.Increment(ref _auditReads);
                if (readNumber <= 2)
                {
                    if (readNumber == 2) _bothReadsFinished.TrySetResult();
                    await _bothReadsFinished.Task.WaitAsync(cancellationToken);
                }
            }

            return result;
        }
    }
}
