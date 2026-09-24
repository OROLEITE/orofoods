using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.ViewModels;

public sealed class SellerCheckoutViewModel : CheckoutViewModel
{
    public required int CustomerId { get; init; }
    public required string CustomerCode { get; init; }
    public required string CustomerName { get; init; }
    public string? ErrorMessage { get; set; }
}

public sealed record SellerOrderSuccessViewModel(
    int OrderId,
    string OrderNumber,
    string CustomerName,
    decimal Total,
    string PaymentMethod,
    string Status);