using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Services.Orders;

public sealed class OrderIntegrationService(
    ApplicationDbContext db,
    IErpOrderIntegration erp,
    ILogger<OrderIntegrationService>? logger = null)
{
    public async Task SendAsync(
        int orderId,
        CancellationToken cancellationToken = default,
        IntegrationStatus? expectedIntegrationStatus = null)
    {
        if (expectedIntegrationStatus is not null && expectedIntegrationStatus is not (IntegrationStatus.Pending or IntegrationStatus.Failed))
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var attemptedAt = DateTime.UtcNow;
        var claim = db.Orders
            .Where(order => order.Id == orderId && order.Status == OrderStatus.Approved);
        claim = expectedIntegrationStatus is null
            ? claim.Where(order => order.IntegrationStatus != IntegrationStatus.Processing)
            : claim.Where(order => order.IntegrationStatus == expectedIntegrationStatus.Value);

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
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException("Pedido não encontrado.");
        ErpOrderResult result;
        var attemptId = Guid.NewGuid();
        try
        {
            result = await erp.SendOrderAsync(order, cancellationToken, attemptId);
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

        order.IntegrationStatus = result.Succeeded ? IntegrationStatus.Succeeded : IntegrationStatus.Failed;
        order.ExternalOrderId = result.ExternalOrderId;
        order.ErpOrderNumber = result.ErpOrderNumber;
        order.IntegrationError = result.Error;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
