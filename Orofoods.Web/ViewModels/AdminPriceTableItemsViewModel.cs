namespace Orofoods.Web.ViewModels;

public sealed class AdminPriceTableItemsViewModel
{
    public required PriceTable PriceTable { get; init; }
    public required List<Product> Products { get; init; }
    public required Dictionary<int, PriceTableItem> Prices { get; init; }
}
