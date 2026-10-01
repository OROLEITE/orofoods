using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Orders;

public class AdminOrderService(
    ApplicationDbContext db,
    TimeProvider timeProvider,
    OrderReservationService orderReservationService,
    IPaymentEligibilityService paymentEligibilityService,
    PaymentService paymentService)
{
    public async Task<AdminOrderStatusUpdateResult> UpdateStatusAsync(
        int orderId,
        OrderStatus status,
        string? changedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status)) throw new InvalidOperationException("Status de pedido invalido.");
        var order = await db.Orders
            .Include(x => x.PaymentTerm)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == orderId, cancellationToken);
        if (order.Status == status) return AdminOrderStatusUpdateResult.Success;

        if (status == OrderStatus.Delivered && order.PaymentTerm?.Code == "CARD_ON_DELIVERY")
        {
            var latestCardOnDeliveryPayment = order.Payments
                .Where(x => x.Method == PaymentMethodType.CardOnDelivery)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            if (latestCardOnDeliveryPayment?.Status is not (PaymentStatus.Approved or PaymentStatus.Paid))
            {
                return AdminOrderStatusUpdateResult.PaymentApprovalRequired;
            }
        }

        order.Status = status;
        if (status == OrderStatus.Cancelled)
        {
            await orderReservationService.ReleaseAsync(order, cancellationToken);
        }

        var changedAt = timeProvider.GetUtcNow().UtcDateTime;
        db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = status,
            ChangedAt = changedAt,
            ChangedByUserId = changedByUserId
        });
        await db.SaveChangesAsync(cancellationToken);

        if (status is OrderStatus.Invoiced or OrderStatus.Delivered)
        {
            var customer = await db.Customers.SingleAsync(x => x.Id == order.CustomerId, cancellationToken);
            var validPurchases = await paymentEligibilityService.GetValidPurchaseCountAsync(customer.Id, cancellationToken);
            if (validPurchases >= 3 && customer.CreditReleaseDate is null)
            {
                customer.CreditReleaseDate = changedAt;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        await paymentService.IssueForEligibleStatusAsync(order.Id, status, changedAt, cancellationToken);
        return AdminOrderStatusUpdateResult.Success;
    }
}

public sealed record AdminOrderStatusUpdateResult(bool Succeeded, string? ErrorMessage)
{
    public static AdminOrderStatusUpdateResult Success { get; } = new(true, null);
    public static AdminOrderStatusUpdateResult PaymentApprovalRequired { get; } = new(false, "O pedido utiliza Cartão na Entrega e o pagamento ainda não foi aprovado.");
}
