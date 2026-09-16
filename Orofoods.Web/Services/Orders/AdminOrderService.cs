using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Services.Orders;

public class AdminOrderService(ApplicationDbContext db, TimeProvider timeProvider, OrderReservationService orderReservationService)
{
    public async Task UpdateStatusAsync(
        int orderId,
        OrderStatus status,
        string? changedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status)) throw new InvalidOperationException("Status de pedido invalido.");
        var order = await db.Orders.SingleAsync(x => x.Id == orderId, cancellationToken);
        if (order.Status == status) return;

        order.Status = status;
        if (status == OrderStatus.Cancelled)
        {
            await orderReservationService.ReleaseAsync(order, cancellationToken);
        }

        db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = status,
            ChangedAt = timeProvider.GetUtcNow().UtcDateTime,
            ChangedByUserId = changedByUserId
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
