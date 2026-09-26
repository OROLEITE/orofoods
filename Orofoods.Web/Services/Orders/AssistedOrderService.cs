using Orofoods.Web.Data;
using Orofoods.Web.Services.Commercial;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Pricing;
using Microsoft.AspNetCore.Http;

namespace Orofoods.Web.Services.Orders;

public sealed class AssistedOrderService
{
    private readonly OrderPlacementService orderPlacementService;

    public AssistedOrderService(
        ApplicationDbContext db,
        PriceService priceService,
        IPaymentEligibilityService paymentEligibilityService,
        OrderReservationService orderReservationService)
    {
        orderPlacementService = new OrderPlacementService(db, new CartService(db, priceService), priceService, paymentEligibilityService, orderReservationService);
    }

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
        var result = await orderPlacementService.PlaceLinesAsync(
            customerId,
            userId,
            new OrderPlacementCommand(addressId, paymentTermId, requestedDeliveryDate, notes),
            requestedLines,
            cancellationToken: cancellationToken);
        return result.Succeeded
            ? AssistedOrderResult.Success(result.Order!.Id)
            : AssistedOrderResult.Failure(result.Errors[0]);
    }

    public Task<OrderPlacementResult> PlaceAsync(
        int customerId,
        string userId,
        OrderPlacementCommand command,
        ISession session,
        CartScope? scope = null,
        bool clearCart = true,
        CancellationToken cancellationToken = default) =>
        orderPlacementService.PlaceAsync(customerId, userId, command, session, scope, clearCart, cancellationToken);
}

public sealed record AssistedOrderResult(bool Succeeded, int? OrderId, string? ErrorMessage)
{
    public static AssistedOrderResult Success(int orderId) => new(true, orderId, null);
    public static AssistedOrderResult Failure(string error) => new(false, null, error);
}