using Orofoods.Web.Models;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Orders;

using CustomerModel = Orofoods.Web.Models.Customers.Customer;
using CustomerAddressModel = Orofoods.Web.Models.Customers.CustomerAddress;
using OrderModel = Orofoods.Web.Models.Orders.Order;

namespace Orofoods.Web.ViewModels;

public record RepeatItemVm(int ProductId, string Sku, string Name, string Unit, int Minimum, int Quantity, decimal Price, bool Available, string? Substitute);

public class DashboardVm
{
    public required CustomerModel Customer { get; init; }
    public OrderModel? LastOrder { get; init; }
    public required List<FrequentProduct> FrequentProducts { get; init; }
    public decimal MonthTotal { get; init; }
    public int OpenOrders { get; init; }
}

public class RepeatOrderVm
{
    public required CustomerModel Customer { get; init; }
    public required OrderModel SourceOrder { get; init; }
    public required List<RepeatItemVm> Items { get; init; }
    public required List<CustomerAddressModel> Addresses { get; init; }
    public required List<PaymentTerm> PaymentTerms { get; init; }
}

public class ConfirmOrderVm
{
    public int SourceOrderId { get; set; }
    public int AddressId { get; set; }
    public DateTime RequestedDate { get; set; }
    public int PaymentTermId { get; set; }
    public string Notes { get; set; } = "";
    public int[] ProductIds { get; set; } = [];
    public int[] Quantities { get; set; } = [];
}
