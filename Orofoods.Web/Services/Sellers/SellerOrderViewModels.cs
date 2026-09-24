using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;

namespace Orofoods.Web.Services.Sellers;

public sealed record SellerOrderRow(
    int Id,
    string Number,
    DateTime CreatedAt,
    decimal Total,
    string PaymentMethod,
    OrderStatus Status,
    PaymentStatus? PaymentStatus);

public sealed class SellerOrderHistoryViewModel
{
    public required int CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerCode { get; init; }
    public string? Query { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<SellerOrderRow> Orders { get; init; } = [];
}

public sealed record SellerOrderItemRow(string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal Subtotal);

public sealed class SellerOrderDetailViewModel
{
    public required int CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerCode { get; init; }
    public required SellerOrderRow Order { get; init; }
    public IReadOnlyList<SellerOrderItemRow> Items { get; init; } = [];
    public decimal Total => Order.Total;
    public PaymentStatus? PaymentStatus => Order.PaymentStatus;
}