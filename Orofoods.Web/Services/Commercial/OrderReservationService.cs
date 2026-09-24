using System.Data;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Commercial;

public class OrderReservationService(ApplicationDbContext db)
{
    public async Task<CommercialValidationResult> ReserveAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Id == 0)
        {
            return CommercialValidationResult.Failure("O pedido precisa ser gravado antes da reserva comercial.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await ReserveWithinTransactionAsync(order, cancellationToken);
        if (result.IsValid)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    public Task<CommercialValidationResult> ReserveWithinTransactionAsync(Order order, CancellationToken cancellationToken = default) =>
        ReserveCoreAsync(order, cancellationToken);

    private async Task<CommercialValidationResult> ReserveCoreAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Id == 0)
        {
            return CommercialValidationResult.Failure("O pedido precisa ser gravado antes da reserva comercial.");
        }

        var currentOrder = await db.Orders
            .Include(x => x.Customer)
            .Include(x => x.PaymentTerm)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleAsync(x => x.Id == order.Id, cancellationToken);

        var activeReservations = await db.InventoryReservations
            .AnyAsync(x => x.OrderId == order.Id && x.Status == InventoryReservationStatus.Active, cancellationToken);
        if (activeReservations)
        {
            return CommercialValidationResult.Success();
        }

        var customer = currentOrder.Customer ?? throw new InvalidOperationException("Cliente do pedido nao encontrado.");
        if (UsesCredit(currentOrder) && currentOrder.Total > customer.CreditLimit - customer.CreditUsed)
        {
            return CommercialValidationResult.Failure("O total ultrapassa o cr\u00e9dito dispon\u00edvel.");
        }

        var productIds = currentOrder.Items.Select(x => x.ProductId).Distinct().ToList();
        var inventories = await db.ProductInventories
            .Where(x => productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        foreach (var item in currentOrder.Items)
        {
            if (!inventories.TryGetValue(item.ProductId, out var inventory) || inventory.AvailableQuantity < item.Quantity)
            {
                var productName = item.Product?.Name ?? item.ProductNameSnapshot;
                return CommercialValidationResult.Failure($"Estoque insuficiente para {productName}.");
            }
        }

        foreach (var item in currentOrder.Items)
        {
            var inventory = inventories[item.ProductId];
            inventory.QuantityReserved += item.Quantity;
            db.InventoryReservations.Add(new InventoryReservation
            {
                OrderId = currentOrder.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity
            });
        }

        if (UsesCredit(currentOrder))
        {
            customer.CreditUsed += currentOrder.Total;
        }

        await db.SaveChangesAsync(cancellationToken);
        return CommercialValidationResult.Success();
    }

    public async Task ReleaseAsync(Order order, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var currentOrder = await db.Orders
            .Include(x => x.Customer)
            .Include(x => x.PaymentTerm)
            .SingleAsync(x => x.Id == order.Id, cancellationToken);
        var reservations = await db.InventoryReservations
            .Where(x => x.OrderId == order.Id && x.Status == InventoryReservationStatus.Active)
            .ToListAsync(cancellationToken);
        if (reservations.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var inventories = await db.ProductInventories
            .Where(x => reservations.Select(r => r.ProductId).Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var releasedAt = DateTime.UtcNow;
        foreach (var reservation in reservations)
        {
            inventories[reservation.ProductId].QuantityReserved -= reservation.Quantity;
            reservation.Status = InventoryReservationStatus.Released;
            reservation.ReleasedAt = releasedAt;
        }

        if (UsesCredit(currentOrder))
        {
            var customer = currentOrder.Customer ?? throw new InvalidOperationException("Cliente do pedido nao encontrado.");
            customer.CreditUsed = Math.Max(0, customer.CreditUsed - currentOrder.Total);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool UsesCredit(Order order) => order.PaymentTerm is not null
        ? order.PaymentTerm.DaysUntilDue > 0
        : !string.Equals(order.PaymentMethod, "PIX", StringComparison.OrdinalIgnoreCase);
}
