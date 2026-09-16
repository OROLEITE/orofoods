namespace Orofoods.Web.ViewModels;

public class PublicProductCatalogViewModel
{
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<ProductCategory> Categories { get; init; } = [];
    public string? Search { get; init; }
    public string? Category { get; init; }
}
