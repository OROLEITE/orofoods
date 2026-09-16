namespace Orofoods.Web.ViewModels;

public class OrderHistoryViewModel
{
    public IReadOnlyList<Order> Orders { get; init; } = [];
    public string? Query { get; init; }
    public string? Status { get; init; }
}
