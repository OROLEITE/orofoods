using Orofoods.Web.Models.Commercial;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.ViewModels;

public sealed class CustomerCommercialViewModel
{
    public Customer Customer { get; init; } = null!;
    public IReadOnlyList<Order> Orders { get; init; } = [];
    public IReadOnlyList<CommercialActivity> Activities { get; init; } = [];
    public IReadOnlyList<CustomerProductPurchaseViewModel> PurchasedProducts { get; init; } = [];
    public IReadOnlyList<Payment> Payments { get; init; } = [];
    public IReadOnlyList<PriceTableItem> PriceTableItems { get; init; } = [];
    public IReadOnlyList<CustomerUserViewModel> Users { get; init; } = [];
    public IReadOnlyList<CustomerTimelineItemViewModel> Timeline { get; init; } = [];
    public decimal TotalPurchased { get; init; }
    public DateTime? LastPurchaseAt { get; init; }
    public CommercialActivity? NextActivity { get; init; }
    public int OrderCount { get; init; }
    public decimal AverageTicket { get; init; }
    public decimal OutstandingAmount { get; init; }
    public decimal OverdueAmount { get; init; }
    public CommercialAttentionCustomerViewModel? Attention { get; init; }
    public IReadOnlyList<CrmOpportunity> Opportunities { get; init; } = [];
}

public sealed record CustomerProductPurchaseViewModel(
    string Sku,
    string Name,
    int TotalQuantity,
    int OrderCount,
    DateTime LastPurchaseAt,
    decimal TotalAmount,
    decimal AverageUnitPrice);

public sealed record CustomerUserViewModel(string Email, string? PhoneNumber, bool IsActive, string Roles);

public sealed record CustomerTimelineItemViewModel(DateTime OccurredAt, string Title, string Description, string Icon);