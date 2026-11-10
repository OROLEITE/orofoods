namespace Orofoods.Web.Services.Sellers;

public sealed record SellerCatalogProductRow(
    int Id,
    string Sku,
    string Name,
    string Description,
    string UnitDescription,
    decimal UnitPrice,
    int MinimumCases,
    int? ImageId,
    string? Ean = null,
    string PriceUnitLabel = "caixa",
    string? Ean13 = null,
    string? Dun14 = null,
    string? WmcCode = null,
    string Brand = "",
    string CommercialUnitCode = "",
    int AvailableQuantity = 0);

public sealed class SellerCatalogViewModel
{
    public required int CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public string? Query { get; init; }
    public int CartItemCount { get; set; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public IReadOnlyList<SellerCatalogProductRow> Products { get; init; } = [];
}
