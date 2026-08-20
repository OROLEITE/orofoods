using Orofoods.Web.Models;

namespace Orofoods.Web.ViewModels;

public record RepeatItemVm(int ProductId, string Sku, string Name, string Unit, int Minimum, int Quantity, decimal Price, bool Available, string? Substitute);

public class DashboardVm
{
    public required Customer Customer { get; init; }
    public required Order LastOrder { get; init; }
    public decimal MonthTotal { get; init; }
    public int OpenOrders { get; init; }
}

public class RepeatOrderVm
{
    public required Customer Customer { get; init; }
    public required Order SourceOrder { get; init; }
    public required List<RepeatItemVm> Items { get; init; }
    public required List<CustomerAddress> Addresses { get; init; }
}

public class ConfirmOrderVm
{
    public int SourceOrderId { get; set; }
    public int AddressId { get; set; }
    public DateTime RequestedDate { get; set; }
    public string PaymentMethod { get; set; } = "PIX";
    public string Notes { get; set; } = "";
    public int[] ProductIds { get; set; } = [];
    public int[] Quantities { get; set; } = [];
}
