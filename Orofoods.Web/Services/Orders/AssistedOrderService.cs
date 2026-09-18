using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Services.Orders;

public sealed class AssistedOrderService(
    ApplicationDbContext db,
    PriceService priceService,
    IPaymentEligibilityService paymentEligibilityService,
    OrderReservationService orderReservationService)
{
    public async Task<AssistedOrderResult> CreateAsync(
        int customerId,
        string userId,
        int addressId,
        int paymentTermId,
        DateTime requestedDeliveryDate,
        string? notes,
        IReadOnlyList<(int ProductId, int Quantity)> requestedLines,
        CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive && x.Status == CustomerStatus.Approved, cancellationToken);
        if (customer is null) return AssistedOrderResult.Failure("Cliente não encontrado ou não está aprovado.");
        if (!customer.Addresses.Any(x => x.Id == addressId && x.IsActive)) return AssistedOrderResult.Failure("Endereço de entrega inválido.");

        var eligibility = await paymentEligibilityService.ValidateAsync(customerId, paymentTermId, cancellationToken);
        if (!eligibility.IsAllowed) return AssistedOrderResult.Failure(eligibility.ErrorMessage!);

        var term = await db.PaymentTerms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == paymentTermId && x.IsActive, cancellationToken);
        if (term is null) return AssistedOrderResult.Failure("Condição de pagamento inválida.");
        if (term.DaysUntilDue > 0 && customer.CreditLimit - customer.CreditUsed < 0) return AssistedOrderResult.Failure("O cliente não possui crédito disponível.");

        var lines = requestedLines
            .Where(x => x.ProductId > 0 && x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(x => (ProductId: x.Key, Quantity: x.Sum(item => item.Quantity)))
            .ToList();
        if (lines.Count == 0) return AssistedOrderResult.Failure("Adicione ao menos um produto.");

        var products = await db.Products.AsNoTracking()
            .Where(x => lines.Select(line => line.ProductId).Contains(x.Id) && x.IsActive && x.IsAvailable)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != lines.Count) return AssistedOrderResult.Failure("Um ou mais produtos não estão disponíveis.");

        var prices = await priceService.GetPricesAsync(customerId, lines.Select(x => x.ProductId), cancellationToken);
        var order = new Order
        {
            CustomerId = customerId,
            CreatedByUserId = userId,
            DeliveryAddressId = addressId,
            PaymentTermId = paymentTermId,
            PaymentMethod = term.Name,
            RequestedDeliveryDate = requestedDeliveryDate,
            Notes = notes ?? "",
            Status = OrderStatus.Received,
            CreatedAt = DateTime.UtcNow
        };
        order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Received, ChangedAt = order.CreatedAt, ChangedByUserId = userId });
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var quantity = Math.Max(line.Quantity, product.MinimumCases);
            var unitPrice = prices.GetValueOrDefault(product.Id, product.PromotionalPrice ?? product.BasePrice);
            order.Items.Add(new OrderItem { ProductId = product.Id, ProductNameSnapshot = product.Name, SkuSnapshot = product.Sku, Quantity = quantity, UnitPrice = unitPrice, Subtotal = unitPrice * quantity });
        }
        order.Subtotal = order.Items.Sum(x => x.Subtotal);
        order.Total = order.Subtotal;
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        var reservation = await orderReservationService.ReserveAsync(order, cancellationToken);
        if (!reservation.IsValid)
        {
            db.Orders.Remove(order);
            await db.SaveChangesAsync(cancellationToken);
            return AssistedOrderResult.Failure(reservation.ErrorMessage!);
        }

        order.Number = $"ORO-{DateTime.UtcNow:yyyy}-{order.Id:000000}";
        order.ConfirmedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return AssistedOrderResult.Success(order.Id);
    }
}