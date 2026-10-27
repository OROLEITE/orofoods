using Microsoft.Extensions.Logging.Abstractions;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcCustomerImportServiceTests
{
    [Fact]
    public async Task Preview_filters_by_seller_status_restriction_and_valid_cnpj()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var reader = new FakeCustomerReader([
            Row("C001", "A", "F", "04.252.011/0001-10", "V1"),
            Row("C002", "I", "F", "04.252.011/0001-10", "V1"),
            Row("C003", "A", "B", "04.252.011/0001-10", "V1"),
            Row("C004", "A", "F", "123", "V1"),
            Row("C005", "A", "F", "04.252.011/0001-10", "V2")
        ]);
        var service = CreateService(db, reader);

        var preview = await service.PreviewAsync("V1");

        Assert.Equal(4, preview.RecordsRead);
        Assert.Single(preview.Rows, row => row.Disposition == WmcCustomerImportDisposition.Create);
        Assert.Equal(3, preview.Ignored);
        Assert.Equal("V1", reader.LastSellerCode);
    }

    [Fact]
    public async Task Preview_blocks_duplicate_code_or_cnpj_conflicts()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.AddRange(
            new Customer { WmcCode = "C001", LegalName = "Atual", TradeName = "Atual", Cnpj = "04.252.011/0001-10" },
            new Customer { WmcCode = "OUTRO", LegalName = "Outro", TradeName = "Outro", Cnpj = "11.222.333/0001-81" });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C001", "A", "F", "11.222.333/0001-81", "V1")]));

        var preview = await service.PreviewAsync("V1");

        Assert.Equal(1, preview.Conflicts);
        Assert.All(preview.Rows, row => Assert.Equal(WmcCustomerImportDisposition.Conflict, row.Disposition));
    }

    [Fact]
    public async Task Import_requires_conflict_free_preview_and_preserves_existing_status()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var existing = new Customer
        {
            WmcCode = "C001", LegalName = "Anterior", TradeName = "Anterior", Cnpj = "04.252.011/0001-10",
            Status = CustomerStatus.Blocked, IsActive = false
        };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([
            Row("C001", "A", "F", "04.252.011/0001-10", "V1", "Novo Nome", "Nova Fantasia")
        ]));

        var result = await service.ImportAsync("V1");

        Assert.Equal(1, result.Updated);
        Assert.Equal("Novo Nome", existing.LegalName);
        Assert.Equal("Nova Fantasia", existing.TradeName);
        Assert.Equal(CustomerStatus.Blocked, existing.Status);
        Assert.False(existing.IsActive);
    }

    [Fact]
    public async Task Import_is_idempotent_and_new_customer_stays_pending_and_unavailable()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new FakeCustomerReader([
            Row("C010", "A", "F", "04.252.011/0001-10", "V1")
        ]));

        var first = await service.ImportAsync("V1");
        var second = await service.ImportAsync("V1");

        Assert.Equal(1, first.Created);
        Assert.Equal(2, second.Ignored);
        var customer = Assert.Single(db.Customers);
        Assert.Equal(CustomerStatus.Pending, customer.Status);
        Assert.False(customer.IsActive);
        Assert.DoesNotContain("WMC-", customer.Cnpj);
    }

    [Fact]
    public async Task Preview_blocks_duplicate_rows_from_the_same_wmc_seller()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new FakeCustomerReader([
            Row("C020", "A", "F", "04.252.011/0001-10", "V1"),
            Row("C020", "A", "F", "04.252.011/0001-10", "V1")
        ]));

        var preview = await service.PreviewAsync("V1");

        Assert.Equal(2, preview.Conflicts);
    }

    [Fact]
    public async Task Preview_blocks_existing_code_with_a_different_cnpj()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer { WmcCode = "C021", LegalName = "Atual", TradeName = "Atual", Cnpj = "04.252.011/0001-10" });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C021", "A", "F", "11.222.333/0001-81", "V1")]));

        var preview = await service.PreviewAsync("V1");

        Assert.Equal(1, preview.Conflicts);
    }

    private static WmcCustomerImportService CreateService(ApplicationDbContext db, IWmcCustomerReader reader) =>
        new(db, reader, NullLogger<WmcCustomerImportService>.Instance, TimeProvider.System);

    private static WmcCustomerRecord Row(string code, string situation, string restriction, string cnpj, string seller, string? legalName = null, string? tradeName = null) =>
        new(code, legalName ?? $"Razao {code}", tradeName ?? $"Fantasia {code}", cnpj, seller, situation, restriction);

    private sealed class FakeCustomerReader(IReadOnlyList<WmcCustomerRecord> rows) : IWmcCustomerReader
    {
        public string? LastSellerCode { get; private set; }

        public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);

        public Task<IReadOnlyList<WmcCustomerRecord>> GetBySellerAsync(string sellerCode, CancellationToken cancellationToken = default)
        {
            LastSellerCode = sellerCode;
            return Task.FromResult<IReadOnlyList<WmcCustomerRecord>>(rows.Where(row => row.CodVendedor == sellerCode).ToList());
        }

        public Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(rows.Select(row => row.CodVendedor).Distinct().ToList());
    }
}
