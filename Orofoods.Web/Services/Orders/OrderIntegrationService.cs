using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Services.Orders;

public sealed class OrderIntegrationService(
    ApplicationDbContext db,
    IErpOrderIntegration erp,
    IHostEnvironment environment,
    ILogger<OrderIntegrationService>? logger = null)
{
    public Task SendAsync(int orderId, CancellationToken cancellationToken = default) =>
        SendAsync(orderId, erp, cancellationToken);

    public async Task SendAsync(int orderId, IErpOrderIntegration selectedIntegration, CancellationToken cancellationToken = default)
    {
        if (!IsAllowedEnvironment())
        {
            logger?.LogWarning(
                "Envio ERP do pedido {OrderId} bloqueado no ambiente {Environment}.",
                orderId,
                environment.EnvironmentName);
            return;
        }

        var claim = await db.Orders
            .Where(order => order.Id == orderId &&
                order.ConfirmedAt != null &&
                order.Status != Orofoods.Web.Models.Orders.OrderStatus.Cancelled &&
                (order.IntegrationStatus == IntegrationStatus.Pending || order.IntegrationStatus == IntegrationStatus.Failed))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.IntegrationStatus, IntegrationStatus.Processing)
                .SetProperty(order => order.LastIntegrationAttempt, DateTime.UtcNow)
                .SetProperty(order => order.IntegrationError, (string?)null), cancellationToken);
        if (claim == 0)
        {
            if (!await db.Orders.AnyAsync(order => order.Id == orderId, cancellationToken))
            {
                throw new InvalidOperationException("Pedido não encontrado.");
            }

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
        try
        {
            result = await selectedIntegration.SendOrderAsync(order, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Falha inesperada ao integrar o pedido {OrderNumber} com o ERP.", order.Number);
            order.IntegrationStatus = IntegrationStatus.Failed;
            order.IntegrationError = "Falha inesperada ao enviar o pedido ao ERP.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        order.IntegrationStatus = result.Succeeded
            ? IntegrationStatus.Succeeded
            : result.OutcomeUnknown ? IntegrationStatus.Unknown : IntegrationStatus.Failed;
        order.ExternalOrderId = result.ExternalOrderId;
        order.ErpOrderNumber = result.ErpOrderNumber;
        order.IntegrationError = result.Error;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private bool IsAllowedEnvironment() =>
        environment.IsDevelopment()
        || environment.IsStaging()
        || environment.IsEnvironment("Test");
}
