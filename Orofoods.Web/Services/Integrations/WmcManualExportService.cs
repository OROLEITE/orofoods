using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

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
    WmcExportAuditService auditService)
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

        var state = claim.State!;
        if (!TryPrepareExport(order, out var export, out var error))
        {
            await CompleteAttemptAsync(state, order.Id, userId, userEmail, fileName, false, error, cancellationToken);
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected, fileName, Error: error);
        }

        await CompleteAttemptAsync(state, order.Id, userId, userEmail, fileName, true, null, cancellationToken);
        return new WmcManualExportResult(WmcManualExportDisposition.Exported, fileName, export!.Content);
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

        if (state?.RequiresManualReview == true)
        {
            return new WmcManualExportDiagnostic(false,
                "Confira o histórico anterior e confirme se o arquivo chegou ao WMC antes de continuar.");
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
        if (state is null || state.Status != WmcOrderExportStatus.Exported || state.RequiresManualReview)
        {
            return new WmcManualExportResult(WmcManualExportDisposition.Rejected,
                Error: "Confirme a importação somente depois de uma exportação registrada.");
        }

        state.Status = WmcOrderExportStatus.Imported;
        state.ImportedByUserId = userId;
        state.ImportedByEmail = userEmail;
        state.ImportedAt = DateTime.UtcNow;
        state.UpdatedAt = state.ImportedAt.Value;
        state.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new WmcManualExportResult(WmcManualExportDisposition.Imported, state.FileName);
        }
        catch (DbUpdateConcurrencyException)
        {
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

        if (state.Status is WmcOrderExportStatus.Exported or WmcOrderExportStatus.Imported)
        {
            await RecordBlockedAttemptAsync(order.Id, userId, userEmail, fileName,
                "O pedido já possui uma exportação registrada; uma nova exportação foi bloqueada.", cancellationToken);
            return (null, new WmcManualExportResult(WmcManualExportDisposition.Duplicate, state.FileName,
                Error: "O pedido já foi exportado para o WMC."));
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
                    await CompleteAttemptAsync(state, order.Id, userId, userEmail, fileName, false, state.LastError, cancellationToken);
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
        await auditService.RecordAsync(orderId, userId, userEmail, fileName, succeeded, error, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private bool TryPrepareExport(Order order, out WmcOrderFileResult? export, out string? error)
    {
        export = null;
        if (!TryValidateExport(order, out error))
        {
            return false;
        }

        export = fileGenerator.Build(order);
        if (!export.Succeeded)
        {
            error = string.Join(" ", export.Errors);
            export = null;
            return false;
        }

        error = null;
        return true;
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
