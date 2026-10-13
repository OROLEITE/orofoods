using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Integrations;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcManualExportServiceTests
{
    [Fact]
    public async Task CanManuallyExport_matches_server_order_eligibility()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);

        Assert.True(service.CanManuallyExport(order));

        order.PaymentTerm!.Code = "UNSUPPORTED";
        Assert.False(service.CanManuallyExport(order));

        order.PaymentTerm.Code = "PIX";
        order.Items.Single().Product!.WmcCode = null;
        Assert.False(service.CanManuallyExport(order));

        order.Items.Single().Product!.WmcCode = "610601552";
        order.Status = OrderStatus.Received;
        Assert.False(service.CanManuallyExport(order));
    }

    [Fact]
    public async Task DiagnoseManualExport_reports_exact_payment_block_without_writing_state_or_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.PaymentTerm!.Code = "UNSUPPORTED";
        var service = CreateService(db);
        var result = service.DiagnoseManualExport(order);

        Assert.False(result.CanExport);
        Assert.Equal("WMC_PAYMENT_TERM_UNSUPPORTED", result.BlockReason);
        Assert.Empty(await db.WmcOrderExportStates.ToListAsync());
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

    [Fact]
    public async Task DiagnoseManualExport_reports_eligible_order_without_creating_export_records()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);

        var result = CreateService(db).DiagnoseManualExport(order);

        Assert.True(result.CanExport);
        Assert.Null(result.BlockReason);
        Assert.Empty(await db.WmcOrderExportStates.ToListAsync());
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

    [Fact]
    public async Task Manual_export_eligibility_and_diagnostic_use_customer_cnpj_instead_of_wmc_code()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.Customer!.WmcCode = null;
        var service = CreateService(db);

        Assert.True(service.CanManuallyExport(order));
        Assert.True(service.DiagnoseManualExport(order).CanExport);

        var export = await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, export.Disposition);
        Assert.Equal("12.345.678/0001-99", order.Customer.Cnpj);
        Assert.Equal("12345678000199", export.Content!.Split(Environment.NewLine)[0].Substring(180, 14));
    }

    [Fact]
    public async Task Successful_export_persists_original_artifact_with_hash_and_shared_audit_attempt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, result.Disposition);
        var artifact = await db.WmcManualExportArtifacts.SingleAsync();
        var state = await db.WmcOrderExportStates.SingleAsync();
        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.Equal(result.Content, artifact.Content);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Content!))), artifact.ContentSha256);
        Assert.Equal(artifact.Id, state.CurrentArtifactId);
        Assert.Equal(artifact.AttemptId, audit.AttemptId);
        Assert.Equal(WmcOrderExportStatus.Exported, state.Status);
    }

    [Fact]
    public async Task Download_returns_verified_original_without_changing_state_or_audits()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        var generated = await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");
        var stateVersion = (await db.WmcOrderExportStates.SingleAsync()).Version;
        var auditCount = await db.WmcExportAudits.CountAsync();

        var downloaded = await service.DownloadAsync(order.Id);

        Assert.Equal(WmcManualExportDisposition.Downloaded, downloaded.Disposition);
        Assert.Equal(generated.FileName, downloaded.FileName);
        Assert.Equal(generated.Content, downloaded.Content);
        Assert.Equal(stateVersion, (await db.WmcOrderExportStates.SingleAsync()).Version);
        Assert.Equal(auditCount, await db.WmcExportAudits.CountAsync());
    }

    [Fact]
    public async Task Download_blocks_missing_or_corrupted_artifact_without_writing_state_or_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        var missing = await service.DownloadAsync(order.Id);
        Assert.Equal(WmcManualExportDisposition.Rejected, missing.Disposition);
        Assert.Empty(await db.WmcOrderExportStates.ToListAsync());

        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");
        var artifact = await db.WmcManualExportArtifacts.SingleAsync();
        artifact.Content += "alterado";
        await db.SaveChangesAsync();
        var version = (await db.WmcOrderExportStates.SingleAsync()).Version;
        var auditCount = await db.WmcExportAudits.CountAsync();

        var corrupted = await service.DownloadAsync(order.Id);

        Assert.Equal(WmcManualExportDisposition.Rejected, corrupted.Disposition);
        Assert.Contains("integridade", corrupted.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(version, (await db.WmcOrderExportStates.SingleAsync()).Version);
        Assert.Equal(auditCount, await db.WmcExportAudits.CountAsync());
    }

    [Fact]
    public async Task Rejected_export_can_only_be_regenerated_with_reason_and_preserves_prior_artifact_and_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        var first = await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");
        var original = await db.WmcManualExportArtifacts.SingleAsync();

        var missingReason = await service.RejectAsync(order.Id, "  ", "admin-2", "admin2@orofoods.local");
        Assert.Equal(WmcManualExportDisposition.Rejected, missingReason.Disposition);
        Assert.Equal(WmcOrderExportStatus.Exported, (await db.WmcOrderExportStates.SingleAsync()).Status);

        var rejected = await service.RejectAsync(order.Id, "Layout rejeitado pelo WMC", "admin-2", "admin2@orofoods.local");
        Assert.Equal(WmcManualExportDisposition.Rejected, rejected.Disposition);
        Assert.Equal(WmcOrderExportStatus.Rejected, (await db.WmcOrderExportStates.SingleAsync()).Status);
        Assert.Equal(2, await db.WmcExportAudits.CountAsync());

        var ordinarySend = await service.ExportAsync(order.Id, "admin-2", "admin2@orofoods.local");
        Assert.Equal(WmcManualExportDisposition.Rejected, ordinarySend.Disposition);
        var blockedRegeneration = await service.RegenerateRejectedAsync(order.Id, null, "admin-2", "admin2@orofoods.local");
        Assert.Equal(WmcManualExportDisposition.Rejected, blockedRegeneration.Disposition);
        Assert.Equal(1, await db.WmcManualExportArtifacts.CountAsync());

        var regenerated = await service.RegenerateRejectedAsync(order.Id, "Layout corrigido para NeoGrid 3.2", "admin-2", "admin2@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, regenerated.Disposition);
        var artifacts = await db.WmcManualExportArtifacts.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, artifacts.Count);
        Assert.Equal(original.Id, artifacts[1].SupersedesArtifactId);
        Assert.Equal("Layout corrigido para NeoGrid 3.2", artifacts[1].RegenerationReason);
        Assert.Equal(first.Content, regenerated.Content);
        var audits = await db.WmcExportAudits.OrderBy(x => x.ExportedAt).ThenBy(x => x.Id).ToListAsync();
        Assert.Equal(WmcExportOutcome.Rejected, audits[1].Outcome);
        Assert.Equal(WmcExportOutcome.Regenerated, audits[2].Outcome);
        Assert.Equal("Layout corrigido para NeoGrid 3.2", audits[2].Error);
        Assert.True(audits[2].Succeeded);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Exported, state.Status);
        Assert.Equal(artifacts[1].Id, state.CurrentArtifactId);
        Assert.Null(state.ImportedAt);
    }

    [Fact]
    public async Task Concurrent_regeneration_claims_create_only_one_replacement_artifact()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"wmc-recovery-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = await AddEligibleOrderAsync(setup);
            orderId = order.Id;
            await CreateService(setup).ExportAsync(orderId, "admin-1", "admin@orofoods.local");
            await CreateService(setup).RejectAsync(orderId, "WMC rejeitou o primeiro arquivo", "admin-1", "admin@orofoods.local");
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var first = CreateService(firstDb).RegenerateRejectedAsync(orderId, "Correção 3.2", "admin-2", "one@orofoods.local");
        var second = CreateService(secondDb).RegenerateRejectedAsync(orderId, "Correção 3.2", "admin-3", "two@orofoods.local");

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Disposition == WmcManualExportDisposition.Exported);
        Assert.Single(results, result => result.Disposition is WmcManualExportDisposition.Processing or WmcManualExportDisposition.Rejected);
        Assert.Equal(2, await firstDb.WmcManualExportArtifacts.CountAsync());
        Assert.Equal(3, await firstDb.WmcExportAudits.CountAsync());
        Assert.Equal(1, await firstDb.WmcExportAudits.CountAsync(audit => audit.Outcome == WmcExportOutcome.Regenerated));
        Assert.Equal(WmcOrderExportStatus.Exported, (await firstDb.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Failed_regeneration_keeps_rejection_and_previous_artifact_and_audit_history()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");
        var originalArtifact = await db.WmcManualExportArtifacts.SingleAsync();
        await service.RejectAsync(order.Id, "Arquivo rejeitado", "admin-1", "admin@orofoods.local");
        var originalAuditCount = await db.WmcExportAudits.CountAsync();
        order.Total = 0;
        await db.SaveChangesAsync();

        var result = await service.RegenerateRejectedAsync(order.Id, "Reprocessamento solicitado", "admin-2", "admin2@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Equal(originalAuditCount + 1, await db.WmcExportAudits.CountAsync());
        Assert.Equal(1, await db.WmcManualExportArtifacts.CountAsync());
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Rejected, state.Status);
        Assert.Equal(originalArtifact.Id, state.CurrentArtifactId);
        var rejectionAudit = await db.WmcExportAudits.SingleAsync(audit => audit.Outcome == WmcExportOutcome.Rejected);
        Assert.Equal("Arquivo rejeitado", rejectionAudit.Error);
        Assert.Null(state.ImportedAt);
    }

    [Fact]
    public async Task Manual_export_diagnostic_reports_invalid_customer_cnpj()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.Customer!.Cnpj = "12.345.678/0001-9";

        var diagnostic = CreateService(db).DiagnoseManualExport(order);

        Assert.False(diagnostic.CanExport);
        Assert.Equal("WMC_CUSTOMER_CNPJ_INVALID", diagnostic.BlockReason);
    }

    [Fact]
    public async Task DiagnoseManualExport_reports_manual_review_state_without_changing_it()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var state = new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Pending,
            RequiresManualReview = true,
            LastError = "Histórico incerto"
        };
        order.WmcOrderExportState = state;
        db.WmcOrderExportStates.Add(state);
        await db.SaveChangesAsync();

        var result = CreateService(db).DiagnoseManualExport(order);

        Assert.False(result.CanExport);
        Assert.Equal("Confira o histórico anterior e confirme se o arquivo chegou ao WMC antes de continuar.", result.BlockReason);
        Assert.Equal("Histórico incerto", (await db.WmcOrderExportStates.SingleAsync()).LastError);
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

    [Fact]
    public async Task DiagnoseManualExport_blocks_successful_legacy_history_even_if_state_says_error()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.WmcOrderExportState = new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Error,
            LastError = "falha posterior"
        };
        db.WmcOrderExportStates.Add(order.WmcOrderExportState);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = Guid.NewGuid(),
            Outcome = WmcExportOutcome.Generated,
            Succeeded = true,
            FileName = "WMC_ORO2026000801.txt"
        });
        await db.SaveChangesAsync();

        var diagnostic = CreateService(db).DiagnoseManualExport(order);

        Assert.False(diagnostic.CanExport);
        Assert.Contains("exportação anterior", diagnostic.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiagnoseManualExport_prioritizes_exported_state_reason_like_manual_send()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.WmcOrderExportState = new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Exported,
            RequiresManualReview = true
        };

        var result = CreateService(db).DiagnoseManualExport(order);

        Assert.False(result.CanExport);
        Assert.Equal("O pedido já foi exportado para o WMC.", result.BlockReason);
        Assert.Empty(await db.WmcExportAudits.ToListAsync());
    }

    [Theory]
    [InlineData(WmcOrderExportStatus.Processing)]
    [InlineData(WmcOrderExportStatus.Exported)]
    [InlineData(WmcOrderExportStatus.Imported)]
    public async Task CanManuallyExport_blocks_non_sendable_export_states(WmcOrderExportStatus status)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.WmcOrderExportState = new WmcOrderExportState { OrderId = order.Id, Status = status };

        Assert.False(CreateService(db).CanManuallyExport(order));
    }

    [Fact]
    public async Task CanManuallyExport_blocks_history_requiring_manual_review()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.WmcOrderExportState = new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Pending,
            RequiresManualReview = true
        };

        Assert.False(CreateService(db).CanManuallyExport(order));
    }

    [Fact]
    public async Task Exports_approved_order_once_and_records_actor_file_and_result()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);

        var first = await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");
        var second = await service.ExportAsync(order.Id, "admin-2", "other@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, first.Disposition);
        Assert.Equal("WMC_ORO2026000801.txt", first.FileName);
        Assert.Contains("021  5  1  CD 000", first.Content);
        Assert.Equal(WmcManualExportDisposition.Duplicate, second.Disposition);
        var audit = await db.WmcExportAudits.SingleAsync(x => x.Succeeded);
        Assert.Equal(order.Id, audit.OrderId);
        Assert.Equal("admin-1", audit.ExportedByUserId);
        Assert.Equal("admin@orofoods.local", audit.ExportedByEmail);
        Assert.Equal(first.FileName, audit.FileName);
        Assert.True(audit.ExportedAt <= DateTime.UtcNow);
        Assert.Equal(WmcOrderExportStatus.Exported, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rejects_unapproved_order_and_records_failure_without_returning_a_file()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db, OrderStatus.Received);

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Null(result.Content);
        Assert.Contains("aprovado", result.Error, StringComparison.OrdinalIgnoreCase);
        var audit = await db.WmcExportAudits.SingleAsync();
        Assert.False(audit.Succeeded);
        Assert.Equal("WMC_ORO2026000801.txt", audit.FileName);
        Assert.Equal(WmcOrderExportStatus.Error, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rejects_unsupported_payment_term_without_returning_a_file()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.PaymentTerm!.Code = "UNSUPPORTED";
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Null(result.Content);
        Assert.Contains("WMC_PAYMENT_TERM_UNSUPPORTED", result.Error);
        Assert.False((await db.WmcExportAudits.SingleAsync()).Succeeded);
    }

    [Fact]
    public async Task Rejects_incomplete_item_data_before_building_the_txt()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        order.Items.Single().ProductNameSnapshot = "";
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, result.Disposition);
        Assert.Null(result.Content);
        Assert.Contains("itens completos", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False((await db.WmcExportAudits.SingleAsync()).Succeeded);
    }

    [Fact]
    public async Task Leaves_legacy_success_audits_unconfirmed_until_an_admin_resolves_them()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            ExportedByUserId = "old-admin",
            ExportedByEmail = "old@orofoods.local",
            ExportedAt = DateTime.UtcNow.AddDays(-1),
            FileName = "WMC_ORO2026000801.txt",
            Succeeded = true
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.ReviewRequired, result.Disposition);
        Assert.Equal(1, await db.WmcExportAudits.CountAsync());
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Pending, state.Status);
        Assert.True(state.RequiresManualReview);
    }

    [Fact]
    public async Task Explicitly_confirming_legacy_export_does_not_confirm_import()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            ExportedAt = DateTime.UtcNow.AddDays(-1),
            FileName = "WMC_ORO2026000801.txt",
            Succeeded = true
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var confirmation = await service.ConfirmPriorExportAsync(order.Id, "reviewer", "reviewer@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Exported, confirmation.Disposition);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Exported, state.Status);
        Assert.False(state.RequiresManualReview);
        Assert.Equal("reviewer", state.ReviewedByUserId);
        Assert.Equal("reviewer", state.ExportedByUserId);
        Assert.Null(state.ImportedAt);
        Assert.Single(await db.WmcExportAudits.ToListAsync());
    }

    [Fact]
    public async Task Explicitly_clearing_legacy_uncertainty_allows_only_a_new_manual_export()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcExportAudits.Add(new WmcExportAudit
        {
            OrderId = order.Id,
            ExportedAt = DateTime.UtcNow.AddDays(-1),
            FileName = "WMC_ORO2026000801.txt",
            Succeeded = false,
            Error = "Falha antiga"
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var review = await service.ConfirmPriorExportDidNotReachWmcAsync(order.Id, "reviewer", "reviewer@orofoods.local");
        var export = await service.ExportAsync(order.Id, "admin-2", "admin2@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Rejected, review.Disposition);
        Assert.Equal(WmcManualExportDisposition.Exported, export.Disposition);
        Assert.Equal(2, await db.WmcExportAudits.CountAsync());
        Assert.Single(await db.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
        Assert.Equal(WmcOrderExportStatus.Exported, (await db.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Import_requires_a_separate_explicit_confirmation()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var confirmation = await service.ConfirmImportedAsync(order.Id, "admin-2", "importer@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Imported, confirmation.Disposition);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Imported, state.Status);
        Assert.Equal("admin-2", state.ImportedByUserId);
        Assert.Equal("importer@orofoods.local", state.ImportedByEmail);
        Assert.NotNull(state.ImportedAt);
        Assert.Equal(2, await db.WmcExportAudits.CountAsync(x => x.Succeeded));
        var importAudit = await db.WmcExportAudits.SingleAsync(x => x.Outcome == WmcExportOutcome.Imported);
        Assert.Equal("admin-2", importAudit.ExportedByUserId);
        Assert.Equal("importer@orofoods.local", importAudit.ExportedByEmail);
        Assert.Equal(state.FileName, importAudit.FileName);
        Assert.True(importAudit.Succeeded);
    }

    [Fact]
    public async Task Repeated_import_confirmation_is_idempotent_and_does_not_duplicate_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        var service = CreateService(db);
        await service.ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        var first = await service.ConfirmImportedAsync(order.Id, "admin-2", "importer@orofoods.local");
        var importedAt = (await db.WmcOrderExportStates.SingleAsync()).ImportedAt;
        var firstAuditCount = await db.WmcExportAudits.CountAsync();
        var retry = await service.ConfirmImportedAsync(order.Id, "admin-2", "importer@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.Imported, first.Disposition);
        Assert.Equal(WmcManualExportDisposition.Imported, retry.Disposition);
        Assert.Equal(importedAt, (await db.WmcOrderExportStates.SingleAsync()).ImportedAt);
        Assert.Equal(firstAuditCount, await db.WmcExportAudits.CountAsync());
        Assert.Single(await db.WmcExportAudits.Where(x => x.Outcome == WmcExportOutcome.Imported).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_import_confirmations_commit_one_audit_and_both_observe_imported_state()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"wmc-import-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        int orderId;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            var order = await AddEligibleOrderAsync(setup);
            orderId = order.Id;
            await CreateService(setup).ExportAsync(orderId, "admin-1", "admin@orofoods.local");
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var first = CreateService(firstDb).ConfirmImportedAsync(orderId, "admin-2", "importer@orofoods.local");
        var second = CreateService(secondDb).ConfirmImportedAsync(orderId, "admin-3", "other-importer@orofoods.local");

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(WmcManualExportDisposition.Imported, result.Disposition));
        Assert.Single(await firstDb.WmcExportAudits.Where(x => x.Outcome == WmcExportOutcome.Imported).ToListAsync());
        Assert.Equal(WmcOrderExportStatus.Imported, (await firstDb.WmcOrderExportStates.SingleAsync()).Status);
    }

    [Fact]
    public async Task Stale_processing_is_recovered_as_error_without_automatic_export()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var order = await AddEligibleOrderAsync(db);
        db.WmcOrderExportStates.Add(new WmcOrderExportState
        {
            OrderId = order.Id,
            Status = WmcOrderExportStatus.Processing,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-30),
            Version = 3
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).ExportAsync(order.Id, "admin-1", "admin@orofoods.local");

        Assert.Equal(WmcManualExportDisposition.RecoveredInterrupted, result.Disposition);
        Assert.Null(result.Content);
        var state = await db.WmcOrderExportStates.SingleAsync();
        Assert.Equal(WmcOrderExportStatus.Error, state.Status);
        Assert.True(state.RequiresManualReview);
        Assert.Empty(await db.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
        Assert.Single(await db.WmcExportAudits.Where(x => !x.Succeeded).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_contexts_cannot_create_two_exports_for_one_order()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"wmc-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
        await using var keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await AddEligibleOrderAsync(setup);
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var orderId = await firstDb.Orders.Select(x => x.Id).SingleAsync();
        var first = CreateService(firstDb).ExportAsync(orderId, "admin-1", "one@orofoods.local");
        var second = CreateService(secondDb).ExportAsync(orderId, "admin-2", "two@orofoods.local");

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, x => x.Disposition == WmcManualExportDisposition.Exported);
        Assert.Single(results, x => x.Disposition is WmcManualExportDisposition.Duplicate or WmcManualExportDisposition.Processing);
        Assert.Single(await firstDb.WmcExportAudits.Where(x => x.Succeeded).ToListAsync());
        Assert.Equal(WmcOrderExportStatus.Exported, (await firstDb.WmcOrderExportStates.SingleAsync()).Status);
    }

    private static WmcManualExportService CreateService(ApplicationDbContext db) =>
        new(db, WmcTestOrderFileGenerator.Create(), new WmcExportAuditService(db));

    private static async Task<Order> AddEligibleOrderAsync(ApplicationDbContext db, OrderStatus status = OrderStatus.Approved)
    {
        var creator = TestDbContextFactory.CreateOrderCreator("order-creator");
        var order = new Order
        {
            Number = "ORO-2026-000801",
            Status = status,
            CreatedAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { LegalName = "Cliente Ltda", TradeName = "Cliente", Cnpj = "12.345.678/0001-99", WmcCode = "107072" },
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
                        Name = "Produto",
                        Sku = "SKU-1",
                        WmcCode = "610601552",
                        Unit = "UN",
                        ProductCategory = new ProductCategory { Name = "Categoria", Slug = "wmc-test-category" }
                    },
                    ProductNameSnapshot = "Produto",
                    SkuSnapshot = "SKU-1",
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
}
