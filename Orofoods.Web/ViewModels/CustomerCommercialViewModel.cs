using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.ViewModels;

public sealed class CustomerCommercialViewModel
{
    public Customer Customer { get; init; } = null!;
    public IReadOnlyList<Order> Orders { get; init; } = [];
    public IReadOnlyList<CommercialActivity> Activities { get; init; } = [];
    public decimal TotalPurchased { get; init; }
    public DateTime? LastPurchaseAt { get; init; }
    public CommercialActivity? NextActivity { get; init; }
}