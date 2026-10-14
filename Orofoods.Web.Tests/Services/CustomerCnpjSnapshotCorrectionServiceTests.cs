using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class CustomerCnpjSnapshotCorrectionServiceTests
{
    [Fact]
    public async Task Corrects_legacy_snapshot_with_audit_and_preserves_customer_and_wmc_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (customer, order) = await CreateOrderAsync(db, "ORO-2026-000010");
        var artifact = new WmcManualExportArtifact
        {
            OrderId = order.Id,
            AttemptId = Guid.NewGuid(),
            FileName = "WMC_ORO-2026-000010.txt",
            Content = "TXT ORIGINAL\r\n",
            ContentSha256 = "hash-original",
            CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        db.WmcManualExportArtifacts.Add(artifact);
        await db.SaveChangesAsync();
        var state = new WmcOrderExportState { OrderId = order.Id, Status = WmcOrderExportStatus.Error, CurrentArtifactId = artifact.Id };
        var oldExportAudit = new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = artifact.AttemptId,
            Outcome = WmcExportOutcome.Failed,
            Succeeded = false,
            FileName = artifact.FileName,
            ExportedAt = artifact.CreatedAt
        };
        db.AddRange(state, oldExportAudit);
        await db.SaveChangesAsync();

        var result = await new CustomerCnpjSnapshotCorrectionService(db).CorrectAsync(
            order.Id, "00.000.000/E08G-12", "Documento fiscal conferido", "admin-id", "admin@orofoods.local");

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("00.000.000/E08G-12", (await db.Orders.SingleAsync(x => x.Id == order.Id)).CustomerCnpjSnapshot);
        Assert.Equal("03.932.292/0001-99", (await db.Customers.SingleAsync(x => x.Id == customer.Id)).Cnpj);
        var persistedArtifact = await db.WmcManualExportArtifacts.SingleAsync();
        Assert.Equal("TXT ORIGINAL\r\n", persistedArtifact.Content);
        Assert.Equal("hash-original", persistedArtifact.ContentSha256);
        var preservedAudit = await db.WmcExportAudits.SingleAsync();
        Assert.Equal(oldExportAudit.AttemptId, preservedAudit.AttemptId);
        Assert.Equal(WmcExportOutcome.Failed, preservedAudit.Outcome);
        var correctionAudit = await db.CustomerCnpjAudits.SingleAsync();
        Assert.Equal(CustomerCnpjAuditAction.OrderSnapshotCorrected, correctionAudit.Action);
        Assert.Equal(order.Id, correctionAudit.OrderId);
        Assert.Equal(customer.Id, correctionAudit.CustomerId);
        Assert.Null(correctionAudit.PreviousCnpj);
        Assert.Equal("00.000.000/E08G-12", correctionAudit.NewCnpj);
        Assert.Equal("Documento fiscal conferido", correctionAudit.Reason);
        Assert.Equal("admin-id", correctionAudit.ChangedByUserId);
    }

    [Theory]
    [InlineData(WmcOrderExportStatus.Exported)]
    [InlineData(WmcOrderExportStatus.Imported)]
    [InlineData(WmcOrderExportStatus.Processing)]
    [InlineData(WmcOrderExportStatus.Rejected)]
    public async Task Blocks_snapshot_correction_after_confirmed_or_in_progress_export(WmcOrderExportStatus status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, order) = await CreateOrderAsync(db, "ORO-2026-000011");
        db.Add(new WmcOrderExportState { OrderId = order.Id, Status = status });
        await db.SaveChangesAsync();

        var result = await new CustomerCnpjSnapshotCorrectionService(db).CorrectAsync(
            order.Id, "03.932.292/0001-99", "Documento conferido", "admin-id", "admin@orofoods.local");

        Assert.False(result.Succeeded);
        Assert.Equal("Cnpj", result.ErrorField);
        Assert.Null((await db.Orders.SingleAsync(x => x.Id == order.Id)).CustomerCnpjSnapshot);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Theory]
    [InlineData("03.932.292/0001-99")]
    [InlineData("00.000.000/E08G-12")]
    public async Task Blocks_changing_an_existing_valid_fiscal_snapshot(string existingSnapshot)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, order) = await CreateOrderAsync(db, "ORO-2026-000014");
        order.CustomerCnpjSnapshot = existingSnapshot;
        await db.SaveChangesAsync();

        var result = await new CustomerCnpjSnapshotCorrectionService(db).CorrectAsync(
            order.Id, "03.932.292/0001-99", "Documento conferido", "admin-id", "admin@orofoods.local");

        Assert.False(result.Succeeded);
        Assert.Empty(await db.WmcOrderExportStates.ToListAsync());
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Fact]
    public async Task Reserves_pending_wmc_state_when_correcting_legacy_snapshot_without_state()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, order) = await CreateOrderAsync(db, "ORO-2026-000015");

        var result = await new CustomerCnpjSnapshotCorrectionService(db).CorrectAsync(
            order.Id, "03.932.292/0001-99", "Documento conferido", "admin-id", "admin@orofoods.local");

        Assert.True(result.Succeeded, result.ErrorMessage);
        var state = await db.WmcOrderExportStates.SingleAsync(x => x.OrderId == order.Id);
        Assert.Equal(WmcOrderExportStatus.Pending, state.Status);
        Assert.Equal("03.932.292/0001-99", (await db.Orders.SingleAsync(x => x.Id == order.Id)).CustomerCnpjSnapshot);
    }

    [Fact]
    public async Task Blocks_snapshot_correction_when_automatic_export_was_made_available()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, order) = await CreateOrderAsync(db, "ORO-2026-000012");
        db.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = Guid.NewGuid(),
            Outcome = WmcExportOutcome.Available,
            Succeeded = true
        });
        await db.SaveChangesAsync();

        var result = await new CustomerCnpjSnapshotCorrectionService(db).CorrectAsync(
            order.Id, "03.932.292/0001-99", "Documento conferido", "admin-id", "admin@orofoods.local");

        Assert.False(result.Succeeded);
        Assert.Null((await db.Orders.SingleAsync(x => x.Id == order.Id)).CustomerCnpjSnapshot);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    [Fact]
    public async Task Rejects_invalid_cnpj_and_missing_justification()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var (_, order) = await CreateOrderAsync(db, "ORO-2026-000013");
        var service = new CustomerCnpjSnapshotCorrectionService(db);

        var invalid = await service.CorrectAsync(order.Id, "00.000.000/E08G-13", "Documento conferido", "admin-id", "admin@orofoods.local");
        var noReason = await service.CorrectAsync(order.Id, "00.000.000/E08G-12", " ", "admin-id", "admin@orofoods.local");

        Assert.False(invalid.Succeeded);
        Assert.Equal("Cnpj", invalid.ErrorField);
        Assert.False(noReason.Succeeded);
        Assert.Equal("Reason", noReason.ErrorField);
        Assert.Null((await db.Orders.SingleAsync(x => x.Id == order.Id)).CustomerCnpjSnapshot);
        Assert.Empty(await db.CustomerCnpjAudits.ToListAsync());
    }

    private static async Task<(Customer Customer, Order Order)> CreateOrderAsync(ApplicationDbContext db, string number)
    {
        var customer = new Customer { LegalName = "Cliente", TradeName = "Cliente", Cnpj = "03.932.292/0001-99" };
        var user = new ApplicationUser { Id = "admin-user", UserName = "admin-user", Email = "admin@orofoods.local", IsActive = true };
        db.AddRange(customer, user);
        await db.SaveChangesAsync();
        var order = new Order
        {
            Number = number,
            CustomerId = customer.Id,
            Customer = customer,
            CustomerCnpjSnapshot = null,
            CreatedByUserId = user.Id
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (customer, order);
    }
}
