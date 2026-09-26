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
    string? Notes,
    string? CheckoutAttemptKey = null);

public sealed record OrderPlacementResult(Order? Order, IReadOnlyList<string> Errors, bool WasIdempotentReplay = false)
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
    private const string CheckoutAttemptIndexName = "IX_Orders_CustomerId_CheckoutAttemptKey";

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

        if (!string.IsNullOrWhiteSpace(command.CheckoutAttemptKey))
        {
            var existingOrder = await FindCheckoutAttemptAsync(customerId, command.CheckoutAttemptKey, cancellationToken);
            if (existingOrder is not null)
            {
                return new OrderPlacementResult(existingOrder, [], WasIdempotentReplay: true);
            }
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
            CheckoutAttemptKey = string.IsNullOrWhiteSpace(command.CheckoutAttemptKey) ? null : command.CheckoutAttemptKey,
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
        try
        {
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
        }
        catch (DbUpdateException exception) when (
            !string.IsNullOrWhiteSpace(command.CheckoutAttemptKey) && IsCheckoutAttemptUniqueViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            DetachOrderGraph(order);

            var existingOrder = await FindCheckoutAttemptAsync(customerId, command.CheckoutAttemptKey, cancellationToken);
            if (existingOrder is null)
            {
                throw;
            }

            return new OrderPlacementResult(existingOrder, [], WasIdempotentReplay: true);
        }

        if (clearCart && session is not null)
        {
            cartService.Clear(session, scope ?? CartScope.CustomerSelfService);
        }

        return new(order, []);
    }

    private Task<Order?> FindCheckoutAttemptAsync(int customerId, string attemptKey, CancellationToken cancellationToken) =>
        db.Orders
            .Include(x => x.Items)
            .Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.CustomerId == customerId && x.CheckoutAttemptKey == attemptKey, cancellationToken);

    private void DetachOrderGraph(Order order)
    {
        foreach (var item in order.Items)
        {
            db.Entry(item).State = EntityState.Detached;
        }

        foreach (var history in order.StatusHistory)
        {
            db.Entry(history).State = EntityState.Detached;
        }

        db.Entry(order).State = EntityState.Detached;
    }

    private static bool IsCheckoutAttemptUniqueViolation(DbUpdateException exception)
    {
        for (var cause = exception.InnerException; cause is not null; cause = cause.InnerException)
        {
            var causeType = cause.GetType();
            if (causeType.FullName == "Npgsql.PostgresException")
            {
                var sqlState = causeType.GetProperty("SqlState")?.GetValue(cause) as string;
                var constraintName = causeType.GetProperty("ConstraintName")?.GetValue(cause) as string;
                if (sqlState == "23505" && constraintName == CheckoutAttemptIndexName)
                {
                    return true;
                }
            }

            if (causeType.FullName == "Microsoft.Data.Sqlite.SqliteException"
                && causeType.GetProperty("SqliteErrorCode")?.GetValue(cause) is int errorCode
                && errorCode == 19
                && cause.Message.Contains("UNIQUE constraint failed: Orders.CustomerId, Orders.CheckoutAttemptKey", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
