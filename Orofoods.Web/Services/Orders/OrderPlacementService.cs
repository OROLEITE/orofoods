using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Services.Orders;

public sealed record OrderPlacementCommand(
    int AddressId,
    int PaymentTermId,
    DateTime RequestedDeliveryDate,
    string? Notes);

public sealed record OrderPlacementResult(Order? Order, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Order is not null && Errors.Count == 0;

    public static OrderPlacementResult Failure(string error) => new(null, [error]);
}

public sealed class OrderPlacementService(
    ApplicationDbContext db,
    CartService cartService,
    PriceService priceService,
    IPaymentEligibilityService paymentEligibilityService,
    OrderReservationService orderReservationService)
{
    public async Task<OrderPlacementResult> PlaceAsync(
        int customerId,
        string createdByUserId,
        OrderPlacementCommand command,
        ISession session,
        CartScope? scope = null,
        bool clearCart = true,
        CancellationToken cancellationToken = default)
    {
        if (scope is { Kind: CartScopeKind.SellerAssisted, CustomerId: not null } && scope.CustomerId != customerId)
        {
            return OrderPlacementResult.Failure("O escopo do carrinho não pertence ao cliente informado.");
        }

        var cart = await cartService.GetAsync(customerId, session, scope ?? CartScope.CustomerSelfService);
        var lines = cart.Items
            .Where(x => x.IsAvailable && x.Quantity > 0)
            .Select(x => (x.ProductId, x.Quantity))
            .ToList();
        return await PlaceLinesAsync(customerId, createdByUserId, command, lines, session, scope, clearCart, cancellationToken);
    }

    public async Task<OrderPlacementResult> PlaceLinesAsync(
        int customerId,
        string createdByUserId,
        OrderPlacementCommand command,
        IReadOnlyList<(int ProductId, int Quantity)> requestedLines,
        ISession? session = null,
        CartScope? scope = null,
        bool clearCart = true,
        CancellationToken cancellationToken = default)
    {
        if (scope is { Kind: CartScopeKind.SellerAssisted, CustomerId: not null } && scope.CustomerId != customerId)
        {
            return OrderPlacementResult.Failure("O escopo do carrinho não pertence ao cliente informado.");
        }

        var customer = await db.Customers
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive && x.Status == CustomerStatus.Approved, cancellationToken);
        if (customer is null)
        {
            return OrderPlacementResult.Failure("Cliente não encontrado ou não está aprovado.");
        }

        if (!customer.Addresses.Any(x => x.Id == command.AddressId && x.IsActive))
        {
            return OrderPlacementResult.Failure("Endereço de entrega inválido.");
        }

        var eligibility = await paymentEligibilityService.ValidateAsync(customerId, command.PaymentTermId, cancellationToken);
        if (!eligibility.IsAllowed)
        {
            return OrderPlacementResult.Failure(eligibility.ErrorMessage!);
        }

        var paymentTerm = await db.PaymentTerms.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.PaymentTermId && x.IsActive, cancellationToken);
        if (paymentTerm is null)
        {
            return OrderPlacementResult.Failure("Condição de pagamento inválida.");
        }

        var lines = requestedLines
            .Where(x => x.ProductId > 0 && x.Quantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(x => (ProductId: x.Key, Quantity: x.Sum(item => item.Quantity)))
            .ToList();
        if (lines.Count == 0)
        {
            return OrderPlacementResult.Failure("Adicione ao menos um produto.");
        }

        var products = await db.Products.AsNoTracking()
            .Where(x => lines.Select(line => line.ProductId).Contains(x.Id) && x.IsActive && x.IsAvailable)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != lines.Count)
        {
            return OrderPlacementResult.Failure("Um ou mais produtos não estão disponíveis.");
        }

        var prices = await priceService.GetPricesAsync(customerId, lines.Select(x => x.ProductId), cancellationToken);
        var order = new Order
        {
            CustomerId = customerId,
            CreatedByUserId = createdByUserId,
            DeliveryAddressId = command.AddressId,
            PaymentTermId = paymentTerm.Id,
            PaymentMethod = paymentTerm.Name,
            RequestedDeliveryDate = command.RequestedDeliveryDate,
            Notes = command.Notes ?? "",
            Status = OrderStatus.Received,
            CreatedAt = DateTime.UtcNow
        };
        order.StatusHistory.Add(new OrderStatusHistory
        {
            Status = OrderStatus.Received,
            ChangedAt = order.CreatedAt,
            ChangedByUserId = createdByUserId
        });

        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var quantity = Math.Max(line.Quantity, product.MinimumCases);
            var unitPrice = prices.GetValueOrDefault(product.Id, product.PromotionalPrice ?? product.BasePrice);
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = product.Sku,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Subtotal = unitPrice * quantity
            });
        }

        order.Subtotal = order.Items.Sum(x => x.Subtotal);
        order.Total = order.Subtotal + order.Freight;
        if (order.Total < customer.MinimumOrder)
        {
            return OrderPlacementResult.Failure($"O pedido mínimo é {customer.MinimumOrder:C}.");
        }

        if (paymentTerm.DaysUntilDue > 0 && order.Total > customer.CreditLimit - customer.CreditUsed)
        {
            return OrderPlacementResult.Failure("O total ultrapassa o crédito disponível.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        var reservation = await orderReservationService.ReserveWithinTransactionAsync(order, cancellationToken);
        if (!reservation.IsValid)
        {
            return OrderPlacementResult.Failure(reservation.ErrorMessage!);
        }

        order.Number = $"ORO-{DateTime.UtcNow:yyyy}-{order.Id:000000}";
        order.ConfirmedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (clearCart && session is not null)
        {
            cartService.Clear(session, scope ?? CartScope.CustomerSelfService);
        }

        return new(order, []);
    }
}