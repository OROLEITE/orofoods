using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Services.Orders;

public sealed record OrderPlacementCommand(
    int PaymentTermId,
    DateTime? RequestedDeliveryDate,
    string? Notes,
    string? CheckoutAttemptKey = null);

public sealed record OrderPlacementResult(
    Order? Order,
    IReadOnlyList<string> Errors,
    bool WasIdempotentReplay = false,
    string? ErrorCode = null)
{
    public bool Succeeded => Order is not null && Errors.Count == 0;

    public static OrderPlacementResult Failure(string error, string? errorCode = null) => new(null, [error], ErrorCode: errorCode);
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

        var result = await PlaceLinesCoreAsync(
            customerId,
            createdByUserId,
            command,
            requestedLines,
            sellerOrderDraftId: null,
            strictDraftLines: false,
            ownsTransaction: true,
            expectedQuote: null,
            cancellationToken);

        if (result.Succeeded && clearCart && session is not null)
        {
            cartService.Clear(session, scope ?? CartScope.CustomerSelfService);
        }

        return result;
    }

    public Task<OrderPlacementResult> PlaceDraftLinesWithinTransactionAsync(
        int customerId,
        string createdByUserId,
        OrderPlacementCommand command,
        int sellerOrderDraftId,
        IReadOnlyList<(int ProductId, int Quantity)> persistedLines,
        OrderPlacementQuote expectedQuote,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A submissão de rascunho exige uma transação externa ativa.");
        }

        if (sellerOrderDraftId <= 0)
        {
            return Task.FromResult(OrderPlacementResult.Failure("O vínculo do rascunho é inválido."));
        }

        if (!string.IsNullOrWhiteSpace(command.CheckoutAttemptKey))
        {
            return Task.FromResult(OrderPlacementResult.Failure("O envio do rascunho não aceita uma chave de checkout."));
        }

        return PlaceLinesCoreAsync(
            customerId,
            createdByUserId,
            command,
            persistedLines,
            sellerOrderDraftId,
            strictDraftLines: true,
            ownsTransaction: false,
            expectedQuote: expectedQuote,
            cancellationToken);
    }

    private async Task<OrderPlacementResult> PlaceLinesCoreAsync(
        int customerId,
        string createdByUserId,
        OrderPlacementCommand command,
        IReadOnlyList<(int ProductId, int Quantity)> requestedLines,
        int? sellerOrderDraftId,
        bool strictDraftLines,
        bool ownsTransaction,
        OrderPlacementQuote? expectedQuote,
        CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive && x.Status == CustomerStatus.Approved, cancellationToken);
        if (customer is null)
        {
            return OrderPlacementResult.Failure("Cliente não encontrado ou não está aprovado.");
        }

        var eligibility = await paymentEligibilityService.ValidateAsync(customerId, command.PaymentTermId, cancellationToken);
        if (!eligibility.IsAllowed)
        {
            return OrderPlacementResult.Failure(eligibility.ErrorMessage!);
        }

        var paymentTerm = await db.PaymentTerms.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.PaymentTermId, cancellationToken);
        if (paymentTerm is null)
        {
            return OrderPlacementResult.Failure("Condição de pagamento inválida.");
        }

        List<(int ProductId, int Quantity)> lines;
        if (strictDraftLines)
        {
            if (requestedLines.Count == 0)
            {
                return OrderPlacementResult.Failure("Adicione ao menos um produto ao rascunho.");
            }

            if (requestedLines.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
            {
                return OrderPlacementResult.Failure("Todos os produtos do rascunho precisam ter uma quantidade válida.");
            }

            if (requestedLines.Select(x => x.ProductId).Distinct().Count() != requestedLines.Count)
            {
                return OrderPlacementResult.Failure("O rascunho contém produtos duplicados. Salve-o novamente antes do envio.");
            }

            lines = requestedLines.ToList();
        }
        else
        {
            lines = requestedLines
                .Where(x => x.ProductId > 0 && x.Quantity > 0)
                .GroupBy(x => x.ProductId)
                .Select(x => (ProductId: x.Key, Quantity: x.Sum(item => item.Quantity)))
                .ToList();
        }

        if (lines.Count == 0)
        {
            return OrderPlacementResult.Failure("Adicione ao menos um produto.");
        }

        var productIds = lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != lines.Count)
        {
            return OrderPlacementResult.Failure("Um ou mais produtos não estão disponíveis.");
        }

        if (strictDraftLines && lines.Any(line => line.Quantity < products[line.ProductId].MinimumSalesQuantity))
        {
            return OrderPlacementResult.Failure("A quantidade de um ou mais produtos está abaixo do mínimo de caixas.");
        }

        var prices = await priceService.GetPricesAsync(customerId, productIds, cancellationToken);
        if (productIds.Any(productId => !prices.TryGetValue(productId, out var price) || price <= 0m))
        {
            return OrderPlacementResult.Failure("Um ou mais produtos nÃ£o possuem preÃ§o comercial vÃ¡lido.");
        }
        var order = new Order
        {
            CustomerId = customerId,
            CustomerCnpjSnapshot = customer.Cnpj,
            SalesRepresentativeId = customer.SalesRepresentativeId,
            SellerOrderDraftId = sellerOrderDraftId,
            CheckoutAttemptKey = string.IsNullOrWhiteSpace(command.CheckoutAttemptKey) ? null : command.CheckoutAttemptKey,
            CreatedByUserId = createdByUserId,
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
            var quantity = strictDraftLines ? line.Quantity : Math.Max(line.Quantity, product.MinimumSalesQuantity);
            var unitPrice = prices[product.Id];
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                SkuSnapshot = product.Sku,
                SalesUnitCodeSnapshot = product.EffectiveSalesUnitCode,
                Quantity = quantity,
                UnitPrice = unitPrice,
                Subtotal = unitPrice * quantity
            });
        }

        order.Subtotal = order.Items.Sum(x => x.Subtotal);
        order.Total = order.Subtotal + order.Freight;
        if (expectedQuote is not null && !MatchesQuote(expectedQuote, order, command))
        {
            return OrderPlacementResult.Failure(
                "Os preços ou totais mudaram desde a cotação exibida. Nenhum pedido foi enviado e o rascunho não foi alterado.",
                OrderPlacementErrorCodes.CommercialQuoteChanged);
        }

        var configuredMinimum = await db.OrderMinimumSettings.AsNoTracking()
            .Select(x => (decimal?)x.MinimumAmount)
            .SingleOrDefaultAsync(cancellationToken);
        var minimumOrder = OrderMinimumPolicy.Resolve(configuredMinimum, customer.MinimumOrder);
        if (!OrderMinimumPolicy.MeetsMinimum(order.Subtotal, minimumOrder))
        {
            return OrderPlacementResult.Failure($"O valor mínimo dos itens é {minimumOrder:C}, após os descontos e antes do frete.");
        }

        if (paymentTerm.DaysUntilDue > 0 && order.Total > customer.CreditLimit - customer.CreditUsed)
        {
            return OrderPlacementResult.Failure("O total ultrapassa o crédito disponível.");
        }

        try
        {
            await using var transaction = ownsTransaction
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            db.Orders.Add(order);
            await db.SaveChangesAsync(cancellationToken);

            var reservation = await orderReservationService.ReserveWithinTransactionAsync(order, cancellationToken);
            if (!reservation.IsValid)
            {
                return OrderPlacementResult.Failure(reservation.ErrorMessage!);
            }

            order.Number = $"ORO-{DateTime.UtcNow:yyyy}-{order.Id:000000}";
            order.ConfirmedAt = DateTime.UtcNow;
            if (paymentTerm.Code == "CARD_ON_DELIVERY")
            {
                order.Payments.Add(new Payment
                {
                    CustomerId = customerId,
                    PaymentMethod = paymentTerm.Code,
                    Method = PaymentMethodType.CardOnDelivery,
                    Amount = order.Total,
                    Status = PaymentStatus.Pending,
                    CreatedAt = order.ConfirmedAt.Value,
                    UpdatedAt = order.ConfirmedAt.Value
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (
            ownsTransaction && !string.IsNullOrWhiteSpace(command.CheckoutAttemptKey) && IsCheckoutAttemptUniqueViolation(exception))
        {
            DetachOrderGraph(order);

            var existingOrder = await FindCheckoutAttemptAsync(customerId, command.CheckoutAttemptKey, cancellationToken);
            if (existingOrder is null)
            {
                throw;
            }

            return new OrderPlacementResult(existingOrder, [], WasIdempotentReplay: true);
        }

        return new(order, []);
    }

    private Task<Order?> FindCheckoutAttemptAsync(int customerId, string attemptKey, CancellationToken cancellationToken) =>
        db.Orders
            .Include(x => x.Items)
            .Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.CustomerId == customerId && x.CheckoutAttemptKey == attemptKey, cancellationToken);

    private static bool MatchesQuote(OrderPlacementQuote quote, Order order, OrderPlacementCommand command)
    {
        if (quote.CustomerId != order.CustomerId
            || quote.PaymentTermId != command.PaymentTermId
            || quote.RequestedDeliveryDate != command.RequestedDeliveryDate
            || quote.Subtotal != order.Subtotal
            || quote.Freight != order.Freight
            || quote.Total != order.Total
            || quote.Lines.Count != order.Items.Count)
        {
            return false;
        }

        var expectedLines = quote.Lines.OrderBy(line => line.ProductId).ToArray();
        var actualLines = order.Items.OrderBy(item => item.ProductId).ToArray();
        for (var index = 0; index < expectedLines.Length; index++)
        {
            var expected = expectedLines[index];
            var actual = actualLines[index];
            if (expected.ProductId != actual.ProductId
                || expected.Quantity != actual.Quantity
                || expected.UnitPrice != actual.UnitPrice
                || expected.RequestedDiscountAmount != 0m
                || expected.Subtotal != actual.Subtotal)
            {
                return false;
            }
        }

        return true;
    }

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
