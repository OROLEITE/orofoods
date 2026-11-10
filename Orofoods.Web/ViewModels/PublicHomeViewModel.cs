using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.ViewModels;

public sealed class PublicHomeViewModel
{
    public IReadOnlyList<ProductCategory> Categories { get; init; } = [];
    public IReadOnlyList<Product> Products { get; init; } = [];
}
