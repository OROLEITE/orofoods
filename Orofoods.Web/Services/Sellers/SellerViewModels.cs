using Orofoods.Web.Models.Customers;

namespace Orofoods.Web.Services.Sellers;

public sealed record SellerCustomerRow(
    int Id,
    string Code,
    string Name,
    string City,
    CustomerStatus Status);

public sealed class SellerDashboardViewModel
{
    public required string SellerName { get; init; }
    public int ActiveCustomerCount { get; init; }
    public IReadOnlyList<SellerCustomerRow> Customers { get; init; } = [];
}

public sealed class SellerCustomerListViewModel
{
    public string? Query { get; init; }
    public IReadOnlyList<SellerCustomerRow> Customers { get; init; } = [];
}

public sealed class SellerCustomerWorkspaceViewModel
{
    public required SellerCustomerRow Customer { get; init; }
    public required string SalesRepresentativeName { get; init; }
    public IReadOnlyList<string> PaymentTerms { get; init; } = [];
}