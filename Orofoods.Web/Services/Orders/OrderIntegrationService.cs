using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Services.Orders;

public sealed class OrderIntegrationService(
    ApplicationDbContext db,
    IErpOrderIntegration erp,
    ILogger<OrderIntegrationService>? logger = null)
{
    public async Task SendAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException("Pedido não encontrado.");
        order.IntegrationStatus = IntegrationStatus.Processing;
        order.LastIntegrationAttempt = DateTime.UtcNow;
        order.IntegrationError = null;
        await db.SaveChangesAsync(cancellationToken);

        ErpOrderResult result;
        try
        {
            result = await erp.SendOrderAsync(order, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Falha inesperada ao integrar o pedido {OrderNumber} com o ERP.", order.Number);
            order.IntegrationStatus = IntegrationStatus.Failed;
            order.IntegrationError = "Falha inesperada ao enviar o pedido ao ERP.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        order.IntegrationStatus = result.Succeeded ? IntegrationStatus.Succeeded : IntegrationStatus.Failed;
        order.ExternalOrderId = result.ExternalOrderId;
        order.ErpOrderNumber = result.ErpOrderNumber;
        order.IntegrationError = result.Error;
        await db.SaveChangesAsync(cancellationToken);
    }
}
