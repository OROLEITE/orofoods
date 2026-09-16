namespace Orofoods.Web.ViewModels;

public sealed class CartViewModel
{
    public List<CartLineViewModel> Items { get; init; } = [];
    public decimal Subtotal => Items.Where(x => x.IsAvailable).Sum(x => x.Subtotal);
    public decimal MinimumOrder { get; init; }
    public decimal RemainingForMinimum => Math.Max(0, MinimumOrder - Subtotal);
    public decimal Total => Subtotal;
}

public sealed record CartLineViewModel(int ProductId, string Sku, string Name, string UnitDescription, int Quantity, int MinimumCases, decimal UnitPrice, bool IsAvailable)
{
    public decimal Subtotal => Quantity * UnitPrice;
}
