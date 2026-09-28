namespace Orofoods.Web.Services.Sellers;

public sealed record SellerCatalogProductRow(
    int Id,
    string Sku,
    string Name,
    string Description,
    string UnitDescription,
    decimal UnitPrice,
    int MinimumCases,
    int? ImageId);

public sealed class SellerCatalogViewModel
{
    public required int CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public string? Query { get; init; }
    public int CartItemCount { get; set; }
    public IReadOnlyList<SellerCatalogProductRow> Products { get; init; } = [];
}