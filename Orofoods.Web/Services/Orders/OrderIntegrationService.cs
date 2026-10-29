using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Orders;

public sealed class OrderIntegrationService(
    ApplicationDbContext db,
    IErpOrderIntegration erp,
    IHostEnvironment? environment = null,
    ILogger<OrderIntegrationService>? logger = null,
    IOptions<WmcJsonOrderOptions>? jsonOrderOptions = null)
{
    public Task SendAsync(
        int orderId,
        CancellationToken cancellationToken = default,
        IntegrationStatus? expectedIntegrationStatus = null)
    {
        if (expectedIntegrationStatus is not null && expectedIntegrationStatus is not (IntegrationStatus.Pending or IntegrationStatus.Failed))
        {
            return Task.CompletedTask;
        }

        return SendCoreAsync(orderId, erp, cancellationToken, expectedIntegrationStatus);
    }

    public Task SendAsync(int orderId, IErpOrderIntegration selectedIntegration, CancellationToken cancellationToken = default) =>
        SendCoreAsync(orderId, selectedIntegration, cancellationToken, null);

    public async Task<ErpOrderResult?> SendSelectedAsync(
        int orderId,
        IErpOrderIntegration selectedIntegration,
        string? initiatedByUserId = null,
        string? initiatedByEmail = null,
        CancellationToken cancellationToken = default)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var attemptedAt = DateTime.UtcNow;
            var claim = db.Orders
                .Where(order => order.Id == orderId
                    && order.Status == OrderStatus.Approved
                    && (order.IntegrationStatus == IntegrationStatus.Pending || order.IntegrationStatus == IntegrationStatus.Failed))
                .Where(order => !db.WmcOrderExportStates.Any(state => state.OrderId == order.Id
                    && (state.RequiresManualReview
                        || state.Status == WmcOrderExportStatus.Processing
                        || state.Status == WmcOrderExportStatus.Imported
                        || state.Status == WmcOrderExportStatus.Exported)))
                .Where(order => !db.WmcExportAudits.Any(audit => audit.OrderId == order.Id
                    && (audit.Outcome == WmcExportOutcome.Started
                        || audit.Outcome == WmcExportOutcome.Unknown
                        || audit.Outcome == WmcExportOutcome.Succeeded
                        || audit.Outcome == WmcExportOutcome.Imported
                        || audit.Outcome == WmcExportOutcome.Generated
                        || audit.Outcome == WmcExportOutcome.Available
                        || audit.Outcome == WmcExportOutcome.Regenerated)));

            var claimed = await claim.ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.IntegrationStatus, IntegrationStatus.Processing)
                .SetProperty(order => order.LastIntegrationAttempt, attemptedAt)
                .SetProperty(order => order.IntegrationError, (string?)null), cancellationToken);
            if (claimed == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        db.ChangeTracker.Clear();
        var order = await db.Orders
            .Include(item => item.Customer)
            .Include(item => item.PaymentTerm)
            .Include(item => item.Items).ThenInclude(item => item.Product).ThenInclude(item => item!.WmcUnits)
            .Include(item => item.WmcOrderExportState)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new ErpOrderResult(false, Error: "Pedido não encontrado após a reserva de envio.", OutcomeUnknown: true);
        }

        var attemptId = Guid.NewGuid();
        ErpOrderResult result;
        try
        {
            result = selectedIntegration is WmcJsonOrderErpIntegration jsonIntegration
                ? await jsonIntegration.SendManualOrderAsync(order, initiatedByUserId, initiatedByEmail, cancellationToken, attemptId)
                : await selectedIntegration.SendOrderAsync(order, cancellationToken, attemptId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Falha inesperada ao integrar o pedido {OrderNumber} com o ERP.", order.Number);
            result = new ErpOrderResult(false, Error: "O resultado do envio é incerto; não reenvie este pedido.", OutcomeUnknown: true);
        }

        order.IntegrationStatus = result.Succeeded
            ? IntegrationStatus.Succeeded
            : result.OutcomeUnknown ? IntegrationStatus.Unknown : IntegrationStatus.Failed;
        order.ExternalOrderId = result.ExternalOrderId;
        order.ErpOrderNumber = result.ErpOrderNumber;
        order.IntegrationError = result.Error;

        if (result.Succeeded && selectedIntegration is WmcJsonOrderErpIntegration)
        {
            var now = DateTime.UtcNow;
            order.WmcOrderExportState ??= new WmcOrderExportState { OrderId = order.Id };
            order.WmcOrderExportState.Status = WmcOrderExportStatus.Imported;
            order.WmcOrderExportState.RequiresManualReview = false;
            order.WmcOrderExportState.UpdatedAt = now;
            order.WmcOrderExportState.ImportedAt = now;
            order.WmcOrderExportState.ImportedByUserId = initiatedByUserId;
            order.WmcOrderExportState.ImportedByEmail = initiatedByEmail;
            order.WmcOrderExportState.LastError = result.ErpOrderNumber is null
                ? "Importação confirmada pelo WMC; número do pedido ainda não reconciliado."
                : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<ErpOrderResult?> SendAutomaticJsonAsync(
        int orderId,
        WmcJsonOrderErpIntegration jsonIntegration,
        CancellationToken cancellationToken = default)
    {
        if (environment is not null && !environment.IsDevelopment() && !environment.IsStaging() && !environment.IsEnvironment("Test"))
        {
            logger?.LogWarning("Envio HTTP/JSON WMC do pedido {OrderId} bloqueado no ambiente {Environment}.", orderId, environment.EnvironmentName);
            return null;
        }

        var activationCutoff = jsonOrderOptions?.Value.AutomaticWorkerStartAfterOrderId;
        if (activationCutoff is not int startAfterOrderId || startAfterOrderId < 0)
        {
            logger?.LogError("Automatic WMC JSON claim blocked because the activation cutoff is missing or invalid.");
            return null;
        }

        try
        {
            await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                var claim = WmcJsonOrderCandidateQuery.Build(db, startAfterOrderId)
                    .Where(order => order.Id == orderId);

                var claimed = await claim.ExecuteUpdateAsync(setters => setters
                    .SetProperty(order => order.IntegrationStatus, IntegrationStatus.Processing)
                    .SetProperty(order => order.LastIntegrationAttempt, DateTime.UtcNow)
                    .SetProperty(order => order.IntegrationError, (string?)null), cancellationToken);
                if (claimed == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger?.LogWarning(
                        "Automatic WMC JSON atomic reservation rejected OrderId={OrderId}; reason=EligibilityPredicatesMatchedNoRows.",
                        orderId);
                    return new ErpOrderResult(
                        false,
                        Error: "Automatic WMC JSON atomic reservation predicates matched no rows.",
                        AtomicReservationRejected: true);
                }

                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSafeAutomaticJsonReservationFailure(logger, orderId, exception);
            throw;
        }

        db.ChangeTracker.Clear();
        var order = await db.Orders
            .Include(item => item.Customer)
            .Include(item => item.PaymentTerm)
            .Include(item => item.Items).ThenInclude(item => item.Product).ThenInclude(item => item!.WmcUnits)
            .Include(item => item.WmcOrderExportState)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order is null)
        {
            return new ErpOrderResult(false, Error: "Pedido nÃ£o encontrado apÃ³s a reserva de envio.", OutcomeUnknown: true);
        }

        ErpOrderResult result;
        try
        {
            result = await jsonIntegration.SendAutomaticWorkerOrderAsync(order, cancellationToken, Guid.NewGuid());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSafeAutomaticJsonFailure(logger, orderId, exception);
            result = new ErpOrderResult(false, Error: "O resultado do envio Ã© incerto; nÃ£o reenvie este pedido.", OutcomeUnknown: true);
        }

        order.IntegrationStatus = result.Succeeded
            ? IntegrationStatus.Succeeded
            : result.OutcomeUnknown ? IntegrationStatus.Unknown : IntegrationStatus.Failed;
        order.ExternalOrderId = result.ExternalOrderId;
        order.ErpOrderNumber = result.ErpOrderNumber;
        order.IntegrationError = result.Error;
        if (result.Succeeded)
        {
            var now = DateTime.UtcNow;
            order.WmcOrderExportState ??= new WmcOrderExportState { OrderId = order.Id };
            order.WmcOrderExportState.Status = WmcOrderExportStatus.Imported;
            order.WmcOrderExportState.RequiresManualReview = false;
            order.WmcOrderExportState.UpdatedAt = now;
            order.WmcOrderExportState.ImportedAt = now;
            order.WmcOrderExportState.ImportedByEmail = WmcExportAudit.AutomatedAttemptMarker;
            order.WmcOrderExportState.LastError = result.ErpOrderNumber is null
                ? "ImportaÃ§Ã£o confirmada pelo WMC; nÃºmero do pedido ainda nÃ£o reconciliado."
                : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    internal static void LogSafeAutomaticJsonFailure(
        ILogger<OrderIntegrationService>? logger,
        int orderId,
        Exception exception) => logger?.LogError(
            "Automatic WMC JSON integration failed; OrderId={OrderId}; ErrorType={ErrorType}.",
            orderId,
            exception.GetType().Name);

    internal static void LogSafeAutomaticJsonReservationFailure(
        ILogger<OrderIntegrationService>? logger,
        int orderId,
        Exception exception) => logger?.LogError(
            "Automatic WMC JSON atomic reservation failed; OrderId={OrderId}; ErrorType={ErrorType}.",
            orderId,
            exception.GetType().Name);

    private async Task SendCoreAsync(int orderId, IErpOrderIntegration selectedIntegration, CancellationToken cancellationToken,
        IntegrationStatus? expectedIntegrationStatus)
    {
        if (environment is not null && !environment.IsDevelopment() && !environment.IsStaging() && !environment.IsEnvironment("Test"))
        {
            logger?.LogWarning("Envio ERP do pedido {OrderId} bloqueado no ambiente {Environment}.", orderId, environment.EnvironmentName);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var attemptedAt = DateTime.UtcNow;
        var claim = db.Orders
            .Where(order => order.Id == orderId && order.Status == OrderStatus.Approved);
        claim = expectedIntegrationStatus is null
            ? claim.Where(order => order.IntegrationStatus == IntegrationStatus.Pending || order.IntegrationStatus == IntegrationStatus.Failed)
            : claim.Where(order => order.IntegrationStatus == expectedIntegrationStatus.Value);
        claim = claim.Where(order => !db.WmcOrderExportStates.Any(state =>
            state.OrderId == order.Id && state.Status == WmcOrderExportStatus.Imported));

        var claimed = await claim.ExecuteUpdateAsync(setters => setters
            .SetProperty(order => order.IntegrationStatus, IntegrationStatus.Processing)
            .SetProperty(order => order.LastIntegrationAttempt, attemptedAt)
            .SetProperty(order => order.IntegrationError, (string?)null), cancellationToken);
        if (claimed == 0)
        {
            if (!await db.Orders.AnyAsync(order => order.Id == orderId, cancellationToken))
            {
                throw new InvalidOperationException("Pedido não encontrado.");
            }

            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var order = await db.Orders
            .Include(x => x.Customer)
            .Include(x => x.PaymentTerm)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product).ThenInclude(x => x!.WmcUnits)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException("Pedido não encontrado.");
        ErpOrderResult result;
        var attemptId = Guid.NewGuid();
        try
        {
            result = await selectedIntegration.SendOrderAsync(order, cancellationToken, attemptId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Falha inesperada ao integrar o pedido {OrderNumber} com o ERP.", order.Number);
            order.IntegrationStatus = IntegrationStatus.Failed;
            order.IntegrationError = "Falha inesperada ao enviar o pedido ao ERP.";
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        order.IntegrationStatus = result.Succeeded
            ? IntegrationStatus.Succeeded
            : result.OutcomeUnknown ? IntegrationStatus.Unknown : IntegrationStatus.Failed;
        order.ExternalOrderId = result.ExternalOrderId;
        order.ErpOrderNumber = result.ErpOrderNumber;
        order.IntegrationError = result.Error;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
