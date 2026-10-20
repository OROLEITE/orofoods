using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Services.Integrations;

public enum WmcManualExportDisposition
{
    Exported,
    Imported,
    Rejected,
    Duplicate,
    Processing,
    ReviewRequired,
    RecoveredInterrupted,
    Downloaded,
    NotFound
}

public sealed record WmcManualExportResult(
    WmcManualExportDisposition Disposition,
    string? FileName = null,
    string? Content = null,
    string? Error = null)
{
    public bool Succeeded => Disposition is WmcManualExportDisposition.Exported or WmcManualExportDisposition.Imported;
}

public sealed record WmcManualExportDiagnostic(bool CanExport, string? BlockReason);

public sealed class WmcManualExportService(
    ApplicationDbContext db,
    WmcOrderFileGenerator fileGenerator,
    WmcExportAuditService auditService,
    IPaymentEligibilityService? paymentEligibilityService = null)
{
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(15);

    public async Task<WmcManualExportResult> ExportAsync(
        int orderId,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .Include(x => x.Customer)
            .Include(x => x.PaymentTerm)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.NotFound);
        }

        var fileName = $"WMC_{order.Number.Replace("-", string.Empty)}.txt";
        var claim = await TryClaimAsync(order, fileName, userId, userEmail, cancellationToken);
        if (claim.Result is not null)
        {
            return claim.Result;
        }

        order = await db.Orders.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.PaymentTerm)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.NotFound);
        }

        var state = claim.State!;
        var preparation = await TryPrepareExportAsync(order, cancellationToken);
        if (!preparation.Succeeded)
        {
            await CompleteAttemptAsync(state, order.Id, userId, userEmail, fileName, false, preparation.Error, null, cancellationToken);
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, fileName, Error: preparation.Error);
        }

        await CompleteAttemptAsync(state, order.Id, userId, userEmail, fileName, true, null, preparation.Export, cancellationToken);
        return new WmcManualExportResult(WmcManualExportDisposition.Exported, fileName, preparation.Export!.Content);
    }

    public async Task<WmcManualExportResult> DownloadAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var state = await db.WmcOrderExportStates.AsNoTracking()
            .Include(x => x.CurrentArtifact)
            .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        var artifact = state?.CurrentArtifact;
        if (artifact is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "O arquivo original não está armazenado para download.");
        }

        var order = await db.Orders.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.PaymentTerm)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.NotFound);
        }

        if (order.PaymentTerm is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, artifact.FileName,
                Error: "A condição de pagamento do pedido não está disponível; download bloqueado até confirmar a modalidade.");
        }

        var paymentTermError = WmcOrderFileGenerator.GetPaymentTermExportError(order.PaymentTerm);
        if (paymentTermError is not null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, artifact.FileName,
                Error: paymentTermError);
        }

        var creditAuthorization = await ValidateCreditAuthorizationAsync(order, cancellationToken);
        if (creditAuthorization is not null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, artifact.FileName,
                Error: creditAuthorization);
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artifact.Content)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(artifact.ContentSha256)))
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "A integridade do TXT armazenado não pôde ser confirmada.");
        }

        return new WmcManualExportResult(WmcManualExportDisposition.Downloaded, artifact.FileName, artifact.Content);
    }

    public async Task<WmcManualExportResult> RejectAsync(
        int orderId,
        string? reason,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        reason = NormalizeReason(reason);
        if (reason is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Informe o motivo da rejeição (até 1000 caracteres).");
        }

        var state = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (state is null || state.Status != WmcOrderExportStatus.Exported || state.RequiresManualReview)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Somente uma exportação registrada pode ser marcada como rejeitada.");
        }

        var now = DateTime.UtcNow;
        state.Status = WmcOrderExportStatus.Rejected;
        state.LastError = reason;
        state.UpdatedAt = now;
        state.Version++;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await auditService.RecordAttemptAsync(new WmcExportAudit
            {
                OrderId = orderId,
                AttemptId = Guid.NewGuid(),
                Source = WmcExportSource.Manual,
                Outcome = WmcExportOutcome.Rejected,
                ExportedByUserId = userId,
                ExportedByEmail = userEmail,
                FileName = state.FileName,
                Error = reason,
                GeneratedAt = state.ExportedAt
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, state.FileName);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Processing,
                Error: "O estado WMC mudou durante a marcação da rejeição. Atualize e tente novamente.");
        }
    }

    public async Task<WmcManualExportResult> RegenerateRejectedAsync(
        int orderId,
        string? reason,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        reason = NormalizeReason(reason);
        if (reason is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Informe o motivo da regeneração (até 1000 caracteres).");
        }

        var order = await db.Orders.Include(x => x.Customer).Include(x => x.PaymentTerm)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.NotFound);
        }

        var state = await db.WmcOrderExportStates.Include(x => x.CurrentArtifact)
            .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (state?.Status == WmcOrderExportStatus.Processing)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Processing,
                Error: "Já existe uma regeneração em processamento para este pedido.");
        }
        if (state is null || state.Status != WmcOrderExportStatus.Rejected || state.RequiresManualReview)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "A regeneração só é permitida para um arquivo marcado como rejeitado.");
        }

        var now = DateTime.UtcNow;
        state.Status = WmcOrderExportStatus.Processing;
        state.ProcessingStartedAt = now;
        state.UpdatedAt = now;
        state.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Processing,
                Error: "Outra ação alterou o estado WMC. Atualize a tela antes de regenerar.");
        }

        var preparation = await TryPrepareExportAsync(order, cancellationToken);
        if (!preparation.Succeeded)
        {
            state.Status = WmcOrderExportStatus.Rejected;
            state.ProcessingStartedAt = null;
            state.LastError = preparation.Error;
            state.UpdatedAt = DateTime.UtcNow;
            state.Version++;
            await using var failedTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await auditService.RecordAttemptAsync(new WmcExportAudit
            {
                OrderId = orderId, AttemptId = Guid.NewGuid(), Source = WmcExportSource.Manual,
                Outcome = WmcExportOutcome.Failed, ExportedByUserId = userId, ExportedByEmail = userEmail,
                FileName = state.FileName, Error = $"Regeneração: {reason}. Falha: {preparation.Error}"
            }, cancellationToken);
            await failedTransaction.CommitAsync(cancellationToken);
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, state.FileName, Error: preparation.Error);
        }

        var attemptId = Guid.NewGuid();
        var fileName = $"WMC_{order.Number.Replace("-", string.Empty)}.txt";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var artifact = CreateArtifact(orderId, fileName, preparation.Export!.Content!, attemptId, userId, userEmail,
            state.CurrentArtifactId, reason);
        db.WmcManualExportArtifacts.Add(artifact);
        await db.SaveChangesAsync(cancellationToken);
        state.CurrentArtifactId = artifact.Id;
        state.Status = WmcOrderExportStatus.Exported;
        state.RequiresManualReview = false;
        state.ProcessingStartedAt = null;
        state.FileName = fileName;
        state.ExportedByUserId = userId;
        state.ExportedByEmail = userEmail;
        state.ExportedAt = DateTime.UtcNow;
        state.LastError = null;
        state.UpdatedAt = state.ExportedAt.Value;
        state.Version++;
        await auditService.RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = orderId, AttemptId = attemptId, Source = WmcExportSource.Manual,
            Outcome = WmcExportOutcome.Regenerated, ExportedByUserId = userId, ExportedByEmail = userEmail,
            FileName = fileName, Error = reason, GeneratedAt = state.ExportedAt
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new WmcManualExportResult(WmcManualExportDisposition.Exported, fileName, preparation.Export.Content);
    }

    public bool CanManuallyExport(Order order)
    {
        return DiagnoseManualExport(order).CanExport;
    }

    public WmcManualExportDiagnostic DiagnoseManualExport(Order order)
    {
        var state = order.WmcOrderExportState;
        if (state?.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported)
        {
            return new WmcManualExportDiagnostic(false, "O pedido já foi exportado para o WMC.");
        }

        if (state?.Status == WmcOrderExportStatus.Rejected)
        {
            return new WmcManualExportDiagnostic(false, "O TXT anterior foi rejeitado. Use o fluxo administrativo de regeneração.");
        }

        if (state?.RequiresManualReview == true)
        {
            return new WmcManualExportDiagnostic(false,
                "Confira o histórico anterior e confirme se o arquivo chegou ao WMC antes de continuar.");
        }

        if (order.WmcExportAudits.Any(audit => audit.Succeeded))
        {
            return new WmcManualExportDiagnostic(false,
                "O histórico contém uma exportação anterior; confirme se o arquivo chegou ao WMC antes de continuar.");
        }

        if (state?.Status == WmcOrderExportStatus.Processing)
        {
            return new WmcManualExportDiagnostic(false,
                "Já existe um envio manual em processamento para este pedido.");
        }

        if (state is null && order.WmcExportAudits.Count > 0)
        {
            return new WmcManualExportDiagnostic(false,
                "O histórico anterior não confirma a entrega do arquivo nem sua importação.");
        }

        return TryValidateExport(order, out var error)
            ? new WmcManualExportDiagnostic(true, null)
            : new WmcManualExportDiagnostic(false, error);
    }

    public async Task<WmcManualExportResult> ConfirmImportedAsync(
        int orderId,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        var state = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (state?.Status == WmcOrderExportStatus.Imported)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Imported, state.FileName);
        }

        if (state is null || state.Status != WmcOrderExportStatus.Exported || state.RequiresManualReview)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Confirme a importação somente depois de uma exportação registrada.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        state.Status = WmcOrderExportStatus.Imported;
        state.ImportedByUserId = userId;
        state.ImportedByEmail = userEmail;
        state.ImportedAt = DateTime.UtcNow;
        state.UpdatedAt = state.ImportedAt.Value;
        state.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await auditService.RecordAttemptAsync(new WmcExportAudit
            {
                OrderId = orderId,
                AttemptId = Guid.NewGuid(),
                Source = WmcExportSource.Manual,
                Outcome = WmcExportOutcome.Imported,
                ExportedByUserId = userId,
                ExportedByEmail = userEmail,
                FileName = state.FileName
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new WmcManualExportResult(WmcManualExportDisposition.Imported, state.FileName);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.Entry(state).State = EntityState.Detached;
            var currentState = await db.WmcOrderExportStates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
            if (currentState?.Status == WmcOrderExportStatus.Imported)
            {
                return new WmcManualExportResult(WmcManualExportDisposition.Imported, currentState.FileName);
            }

            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "O estado WMC mudou durante a confirmação. Atualize a tela e confira o pedido.");
        }
    }

    public async Task<WmcManualExportResult> ConfirmPriorExportAsync(
        int orderId,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        var state = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (state is null || !state.RequiresManualReview || state.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Não há um histórico incerto que possa ser confirmado como exportado.");
        }

        var lastFileName = await db.WmcExportAudits.AsNoTracking()
            .Where(x => x.OrderId == orderId && x.FileName != null)
            .OrderByDescending(x => x.ExportedAt)
            .Select(x => x.FileName)
            .FirstOrDefaultAsync(cancellationToken);
        state.Status = WmcOrderExportStatus.Exported;
        state.RequiresManualReview = false;
        state.ExportedByUserId = userId;
        state.ExportedByEmail = userEmail;
        state.ExportedAt = DateTime.UtcNow;
        state.FileName = lastFileName ?? state.FileName;
        state.ReviewedByUserId = userId;
        state.ReviewedByEmail = userEmail;
        state.ReviewedAt = state.ExportedAt;
        state.UpdatedAt = state.ExportedAt.Value;
        state.LastError = null;
        state.ProcessingStartedAt = null;
        state.Version++;
        return await SaveReviewAsync(state, WmcManualExportDisposition.Exported, cancellationToken);
    }

    public async Task<WmcManualExportResult> ConfirmPriorExportDidNotReachWmcAsync(
        int orderId,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken = default)
    {
        var state = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (state is null || !state.RequiresManualReview || state.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Não há uma tentativa incerta que possa ser liberada.");
        }

        state.Status = WmcOrderExportStatus.Pending;
        state.RequiresManualReview = false;
        state.ReviewedByUserId = userId;
        state.ReviewedByEmail = userEmail;
        state.ReviewedAt = DateTime.UtcNow;
        state.UpdatedAt = state.ReviewedAt.Value;
        state.ProcessingStartedAt = null;
        state.LastError = null;
        state.Version++;
        return await SaveReviewAsync(state, WmcManualExportDisposition.Rejected, cancellationToken,
            "Histórico conferido. O pedido está pendente de uma exportação manual.");
    }

    private async Task<(WmcOrderExportState? State, WmcManualExportResult? Result)> TryClaimAsync(
        Order order,
        string fileName,
        string? userId,
        string? userEmail,
        CancellationToken cancellationToken)
    {
        var state = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
        if (state is null)
        {
            var hasLegacyHistory = await db.WmcExportAudits.AnyAsync(x => x.OrderId == order.Id, cancellationToken);
            state = new WmcOrderExportState
            {
                OrderId = order.Id,
                Status = hasLegacyHistory ? WmcOrderExportStatus.Pending : WmcOrderExportStatus.Processing,
                RequiresManualReview = hasLegacyHistory,
                ProcessingStartedAt = hasLegacyHistory ? null : DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                FileName = fileName,
                LastError = hasLegacyHistory ? "O histórico anterior não confirma a entrega do arquivo nem sua importação." : null
            };
            db.WmcOrderExportStates.Add(state);
            var createdByThisRequest = false;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                createdByThisRequest = true;
            }
            catch (DbUpdateException)
            {
                db.Entry(state).State = EntityState.Detached;
                state = await db.WmcOrderExportStates.SingleOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
                if (state is null)
                {
                    throw;
                }
            }

            if (state.RequiresManualReview)
            {
                return (null, new WmcManualExportResult(WmcManualExportDisposition.ReviewRequired,
                    state.FileName, Error: state.LastError));
            }
            if (createdByThisRequest)
            {
                return (state, null);
            }
        }

        if (state.Status is WmcOrderExportStatus.Pending or WmcOrderExportStatus.Error
            && await db.WmcExportAudits.AnyAsync(x => x.OrderId == order.Id && x.Succeeded, cancellationToken))
        {
            state.Status = WmcOrderExportStatus.Pending;
            state.RequiresManualReview = true;
            state.LastError = "O histórico contém uma exportação anterior; confirme se o arquivo chegou ao WMC antes de continuar.";
            state.UpdatedAt = DateTime.UtcNow;
            state.Version++;
            await db.SaveChangesAsync(cancellationToken);
            return (null, new WmcManualExportResult(WmcManualExportDisposition.ReviewRequired, state.FileName,
                Error: state.LastError));
        }

        if (state.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported)
        {
            await RecordBlockedAttemptAsync(order.Id, userId, userEmail, fileName,
                "O pedido já possui uma exportação registrada; uma nova exportação foi bloqueada.", cancellationToken);
            return (null, new WmcManualExportResult(WmcManualExportDisposition.Duplicate, state.FileName,
                Error: "O pedido já foi exportado para o WMC."));
        }

        if (state.Status == WmcOrderExportStatus.Rejected)
        {
            return (null, new WmcManualExportResult(WmcManualExportDisposition.Rejected, state.FileName,
                Error: "O TXT foi marcado como rejeitado. Use a ação administrativa de regeneração."));
        }

        if (state.RequiresManualReview)
        {
            return (null, new WmcManualExportResult(WmcManualExportDisposition.ReviewRequired, state.FileName,
                Error: "Confira o histórico anterior e confirme se o arquivo chegou ao WMC antes de continuar."));
        }

        if (state.Status == WmcOrderExportStatus.Processing)
        {
            if (state.ProcessingStartedAt is not null && state.ProcessingStartedAt <= DateTime.UtcNow - ProcessingTimeout)
            {
                state.Status = WmcOrderExportStatus.Error;
                state.RequiresManualReview = true;
                state.LastError = "O processamento foi interrompido. Confirme manualmente se o arquivo foi entregue antes de tentar novamente.";
                state.ProcessingStartedAt = null;
                state.UpdatedAt = DateTime.UtcNow;
                state.Version++;
                try
                {
                    await CompleteAttemptAsync(state, order.Id, userId, userEmail, fileName, false, state.LastError, null, cancellationToken);
                    return (null, new WmcManualExportResult(WmcManualExportDisposition.RecoveredInterrupted,
                        fileName, Error: state.LastError));
                }
                catch (DbUpdateConcurrencyException)
                {
                    await db.Entry(state).ReloadAsync(cancellationToken);
                }
            }

            await RecordBlockedAttemptAsync(order.Id, userId, userEmail, fileName,
                "Já existe um envio manual em processamento para este pedido.", cancellationToken);
            return (null, new WmcManualExportResult(WmcManualExportDisposition.Processing, fileName,
                Error: "Já existe um envio manual em processamento para este pedido."));
        }

        state.Status = WmcOrderExportStatus.Processing;
        state.ProcessingStartedAt = DateTime.UtcNow;
        state.UpdatedAt = state.ProcessingStartedAt.Value;
        state.FileName = fileName;
        state.LastError = null;
        state.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return (state, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(state).ReloadAsync(cancellationToken);
            var disposition = state.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported
                ? WmcManualExportDisposition.Duplicate
                : state.RequiresManualReview
                    ? WmcManualExportDisposition.ReviewRequired
                    : WmcManualExportDisposition.Processing;
            return (null, new WmcManualExportResult(disposition, state.FileName,
                Error: "O estado do pedido mudou durante a tentativa. Atualize a tela e confira o pedido."));
        }
    }

    private async Task CompleteAttemptAsync(
        WmcOrderExportState state,
        int orderId,
        string? userId,
        string? userEmail,
        string fileName,
        bool succeeded,
        string? error,
        WmcOrderFileResult? export,
        CancellationToken cancellationToken)
    {
        state.Status = succeeded ? WmcOrderExportStatus.Exported : WmcOrderExportStatus.Error;
        state.ProcessingStartedAt = null;
        state.UpdatedAt = DateTime.UtcNow;
        state.FileName = fileName;
        state.LastError = error;
        if (succeeded)
        {
            state.ExportedByUserId = userId;
            state.ExportedByEmail = userEmail;
            state.ExportedAt = state.UpdatedAt;
        }
        state.Version++;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var attemptId = Guid.NewGuid();
        if (succeeded)
        {
            var artifact = CreateArtifact(orderId, fileName, export!.Content!, attemptId, userId, userEmail, null, null);
            db.WmcManualExportArtifacts.Add(artifact);
            await db.SaveChangesAsync(cancellationToken);
            state.CurrentArtifactId = artifact.Id;
        }
        await auditService.RecordAttemptAsync(new WmcExportAudit
        {
            OrderId = orderId,
            AttemptId = attemptId,
            Source = WmcExportSource.Manual,
            Outcome = succeeded ? WmcExportOutcome.Generated : WmcExportOutcome.Failed,
            ExportedByUserId = userId,
            ExportedByEmail = userEmail,
            FileName = fileName,
            Succeeded = succeeded,
            Error = error,
            GeneratedAt = succeeded ? state.ExportedAt : null
        }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static WmcManualExportArtifact CreateArtifact(
        int orderId, string fileName, string content, Guid attemptId, string? userId, string? userEmail,
        int? supersedesArtifactId, string? regenerationReason) => new()
    {
        OrderId = orderId,
        AttemptId = attemptId,
        FileName = fileName,
        Content = content,
        ContentSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
        CreatedAt = DateTime.UtcNow,
        CreatedByUserId = userId,
        CreatedByEmail = userEmail,
        SupersedesArtifactId = supersedesArtifactId,
        RegenerationReason = regenerationReason
    };

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 1000 ? null : normalized;
    }

    private Task RecordBlockedAttemptAsync(
        int orderId,
        string? userId,
        string? userEmail,
        string fileName,
        string error,
        CancellationToken cancellationToken) =>
        auditService.RecordAsync(orderId, userId, userEmail, fileName, false, error, cancellationToken);

    private async Task<WmcManualExportResult> SaveReviewAsync(
        WmcOrderExportState state,
        WmcManualExportDisposition disposition,
        CancellationToken cancellationToken,
        string? message = null)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new WmcManualExportResult(disposition, state.FileName, Error: message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, state.FileName,
                Error: "O estado WMC mudou durante a revisão. Atualize a tela e confira o pedido.");
        }
    }

    private async Task<(bool Succeeded, WmcOrderFileResult? Export, string? Error)> TryPrepareExportAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        if (!TryValidateExport(order, out var error))
        {
            return (false, null, error);
        }

        var creditAuthorization = await ValidateCreditAuthorizationAsync(order, cancellationToken);
        if (creditAuthorization is not null)
        {
            return (false, null, creditAuthorization);
        }

        var export = fileGenerator.Build(order);
        if (!export.Succeeded)
        {
            return (false, null, string.Join(" ", export.Errors));
        }

        return (true, export, null);
    }

    private async Task<string?> ValidateCreditAuthorizationAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.PaymentTerm is not { } term
            || WmcOrderFileGenerator.IsCardMode(term)
            || (term.DaysUntilDue <= 0 && !term.Code.StartsWith("BOLETO_", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (paymentEligibilityService is null)
        {
            return "Verificação da autorização WMC não configurada; exportação/download a prazo bloqueado.";
        }

        if (order.PaymentTermId is null)
        {
            return "O pedido a prazo não possui uma condição local associada; exportação/download bloqueado.";
        }

        var authorization = await paymentEligibilityService.ValidateAsync(order.CustomerId, order.PaymentTermId.Value, cancellationToken);
        return authorization.IsAllowed
            ? null
            : $"Prazo bloqueado: {authorization.ErrorMessage ?? "não autorizado pelo WMC"}";
    }

    private bool TryValidateExport(Order order, out string? error)
    {
        if (order.Status != OrderStatus.Approved)
        {
            error = "Somente pedidos aprovados podem ser exportados para o WMC.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(order.Number) || !order.Number.Any(char.IsAsciiDigit))
        {
            error = "O número do pedido é obrigatório para gerar o arquivo WMC.";
            return false;
        }
        if (order.CreatedAt == default)
        {
            error = "A data do pedido é obrigatória para gerar o arquivo WMC.";
            return false;
        }
        if (order.Items.Count == 0 || order.Items.Any(x => x.Quantity <= 0
            || string.IsNullOrWhiteSpace(x.ProductNameSnapshot)
            || string.IsNullOrWhiteSpace(x.Product?.Unit)
            || x.UnitPrice < 0m
            || x.Subtotal < 0m))
        {
            error = "O pedido precisa conter itens completos com descrição, unidade, quantidade e valores válidos para exportação WMC.";
            return false;
        }
        if (order.Total <= 0m)
        {
            error = "O total do pedido precisa ser maior que zero para exportação WMC.";
            return false;
        }

        var generatorErrors = fileGenerator.Validate(order);
        if (generatorErrors.Count > 0)
        {
            error = string.Join(" ", generatorErrors);
            return false;
        }

        error = null;
        return true;
    }
}
