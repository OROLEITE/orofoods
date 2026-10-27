using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Tests.Infrastructure;
using Microsoft.Extensions.Options;

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
    public async Task Import_is_idempotent_and_new_customer_is_approved()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new FakeCustomerReader([
            Row("C010", "A", "F", "04.252.011/0001-10", "V1")
        ]));

        var first = await service.ImportAsync("V1");
        var second = await service.ImportAsync("V1");

        Assert.Equal(1, first.Created);
        Assert.Equal(1, second.Ignored);
        var customer = Assert.Single(db.Customers);
        Assert.Equal(CustomerStatus.Approved, customer.Status);
        Assert.True(customer.IsActive);
        Assert.DoesNotContain("WMC-", customer.Cnpj);
    }

    [Fact]
    public async Task Preview_blocks_when_wmc_seller_is_not_configured()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C100", "A", "F", "04.252.011/0001-10", "V100")]), "Tabela BIMBO");

        var preview = await service.PreviewAsync("V100");

        Assert.Contains(preview.Rows, row => row.Reason!.Contains("vendedor", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, preview.Conflicts);
    }

    [Fact]
    public async Task Preview_reports_duplicate_wmc_seller_configuration_separately()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.AddRange(
            new SalesRepresentative { Name = "Vendedor 1", WmcCode = "28", IsActive = true },
            new SalesRepresentative { Name = "Vendedor 2", WmcCode = "28", IsActive = true });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C104", "A", "F", "04.252.011/0001-10", "28")]), "Tabela BIMBO");

        var preview = await service.PreviewAsync("28");

        Assert.Contains("Mais de um vendedor", Assert.Single(preview.Rows).Reason);
    }

    [Fact]
    public async Task Import_approves_new_customer_and_assigns_wmc_seller_and_bimbo_table()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Vendedor", WmcCode = "V100", IsActive = true };
        var table = new Orofoods.Web.Models.Pricing.PriceTable { Name = "Tabela BIMBO", IsActive = true };
        db.AddRange(seller, table);
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C101", "A", "F", "04.252.011/0001-10", "V100")]), "Tabela BIMBO");

        var result = await service.ImportAsync("V100");

        Assert.Equal(1, result.Created);
        var customer = Assert.Single(db.Customers);
        Assert.Equal(CustomerStatus.Approved, customer.Status);
        Assert.True(customer.IsActive);
        Assert.NotNull(customer.ApprovedAt);
        Assert.Equal(seller.Id, customer.SalesRepresentativeId);
        Assert.Equal(table.Id, customer.PriceTableId);
    }

    [Fact]
    public async Task Reimport_regularizes_pending_customer_without_overwriting_valid_commercial_data()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Vendedor", WmcCode = "V100", IsActive = true };
        var table = new Orofoods.Web.Models.Pricing.PriceTable { Name = "Tabela BIMBO", IsActive = true };
        var existingTable = new Orofoods.Web.Models.Pricing.PriceTable { Name = "Tabela Existente", IsActive = true };
        var customer = new Customer { WmcCode = "C102", LegalName = "Antiga", TradeName = "Antiga", Cnpj = "04.252.011/0001-10", Status = CustomerStatus.Pending, IsActive = false, PriceTable = existingTable };
        db.AddRange(seller, table, existingTable, customer);
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C102", "A", "F", "04.252.011/0001-10", "V100", "Nova", "Nova")]), "Tabela BIMBO");

        var result = await service.ImportAsync("V100");

        Assert.Equal(1, result.Updated);
        var updated = await db.Customers.SingleAsync();
        Assert.Equal(CustomerStatus.Approved, updated.Status);
        Assert.True(updated.IsActive);
        Assert.Equal(existingTable.Id, updated.PriceTableId);
        Assert.Equal(seller.Id, updated.SalesRepresentativeId);
    }

    [Fact]
    public async Task Regularization_requires_confirmation_and_only_releases_pending_customers()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Vendedor", WmcCode = "V100", IsActive = true };
        var table = new Orofoods.Web.Models.Pricing.PriceTable { Name = "Tabela BIMBO", IsActive = true };
        var customer = new Customer { WmcCode = "C103", LegalName = "Pendente", TradeName = "Pendente", Cnpj = "04.252.011/0001-10", Status = CustomerStatus.Pending, IsActive = false };
        db.AddRange(seller, table, customer);
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([Row("C103", "A", "F", "04.252.011/0001-10", "V100")]), "Tabela BIMBO");

        var preview = await service.PreviewPendingAsync();
        var refused = await service.RegularizePendingAsync(false);
        var result = await service.RegularizePendingAsync(true);

        Assert.Equal(1, preview.EligibleCustomers);
        Assert.Contains("Confirme", refused.ErrorMessage);
        Assert.Equal(1, result.Updated);
        Assert.Equal(CustomerStatus.Approved, (await db.Customers.SingleAsync()).Status);
    }

    [Fact]
    public async Task Regularization_recognizes_active_seller_wmc_code_28_after_reload()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var seller = new SalesRepresentative { Name = "Rodrigo", WmcCode = " 28 ", IsActive = true };
        var table = new Orofoods.Web.Models.Pricing.PriceTable { Name = "TABELA BIMBO", IsActive = true };
        db.AddRange(seller, table, new Customer
        {
            WmcCode = "C-28",
            LegalName = "Cliente 28",
            TradeName = "Cliente 28",
            Cnpj = "04.252.011/0001-10",
            Status = CustomerStatus.Pending,
            IsActive = false
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db, new FakeCustomerReader([Row("C-28", "A", "F", "04.252.011/0001-10", "28")]), "TABELA BIMBO");

        var preview = await service.PreviewPendingAsync();

        Assert.Equal(1, preview.PendingCustomers);
        Assert.Equal(1, preview.EligibleCustomers);
        Assert.Equal(0, preview.BlockedCustomers);
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

    [Fact]
    public async Task Preview_counts_are_mutually_exclusive_and_include_exclusion_reasons()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        db.Customers.Add(new Customer
        {
            WmcCode = "C031",
            LegalName = "Existente",
            TradeName = "Existente",
            Cnpj = "04.252.011/0001-10"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeCustomerReader([
            Row("C030", "A", "F", "03.932.292/0001-99", "V1"),
            Row("C031", "A", "F", "04.252.011/0001-10", "V1", "Existente", "Existente"),
            Row("C032", "I", "F", "04.252.011/0001-10", "V1"),
            Row("C033", "A", "F", "12.345.678/0001-95", "V1", "", "Fantasia")
        ]));

        var preview = await service.PreviewAsync("V1");

        Assert.Equal(4, preview.RecordsRead);
        Assert.Equal(1, preview.NewCustomers);
        Assert.Equal(1, preview.ExistingCustomers);
        Assert.Equal(2, preview.Ignored);
        Assert.Equal(0, preview.Conflicts);
        Assert.Equal(preview.RecordsRead, preview.NewCustomers + preview.ExistingCustomers + preview.Ignored + preview.Conflicts);
        Assert.Contains(preview.Rows, row => row.WmcCode == "C032" && row.Reason == "SITUACAO diferente de A.");
        Assert.Contains(preview.Rows, row => row.WmcCode == "C033" && row.Reason!.Contains("NOME", StringComparison.Ordinal));
        Assert.Contains(preview.Rows, row => row.Disposition == WmcCustomerImportDisposition.Existing);
    }

    [Fact]
    public async Task Preview_returns_a_friendly_error_when_firebird_is_unavailable()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var service = CreateService(db, new UnavailableCustomerReader());

        var preview = await service.PreviewAsync("V1");

        Assert.Contains("clientes WMC", preview.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(preview.Rows);
    }

    private static WmcCustomerImportService CreateService(ApplicationDbContext db, IWmcCustomerReader reader, string? priceTableName = null) =>
        new(db, reader, NullLogger<WmcCustomerImportService>.Instance, TimeProvider.System,
            priceTableName is null ? null : Options.Create(new WmcCustomerImportOptions { PriceTableName = priceTableName }));

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

    private sealed class UnavailableCustomerReader : IWmcCustomerReader
    {
        public Task<IReadOnlyList<WmcCustomerRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror unavailable.");

        public Task<IReadOnlyList<WmcCustomerRecord>> GetBySellerAsync(string sellerCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror unavailable.");

        public Task<IReadOnlyList<string>> GetSellerCodesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Firebird mirror unavailable.");
    }
}
