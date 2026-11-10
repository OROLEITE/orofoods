namespace Orofoods.Web.Services.Orders;

public sealed record OrderPlacementQuote(
    int CustomerId,
    int? DeliveryAddressId,
    int? PaymentTermId,
    DateTime? RequestedDeliveryDate,
    IReadOnlyList<OrderPlacementQuoteLine> Lines,
    decimal Subtotal,
    decimal Freight,
    decimal Total);

public sealed record OrderPlacementQuoteLine(
    int ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal RequestedDiscountAmount,
    decimal Subtotal);

public static class OrderPlacementErrorCodes
{
    public const string CommercialQuoteChanged = "commercial_quote_changed";
}
