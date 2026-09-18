using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.ViewModels;

public sealed class AssistedOrderViewModel
{
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public string SalesRepresentativeName { get; init; } = "";
    public IReadOnlyList<CustomerAddress> Addresses { get; init; } = [];
    public IReadOnlyList<PaymentTerm> PaymentTerms { get; init; } = [];
    public IReadOnlyList<AssistedOrderProductViewModel> Products { get; init; } = [];
    public PaymentEligibilityResult? PaymentEligibility { get; init; }
    public string? Query { get; init; }
    [Range(1, int.MaxValue)] public int AddressId { get; set; }
    [Range(1, int.MaxValue)] public int PaymentTermId { get; set; }
    [DataType(DataType.Date)] public DateTime RequestedDeliveryDate { get; set; } = DateTime.Today.AddDays(1);
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, MaxLength(128)] public string AttemptKey { get; set; } = Guid.NewGuid().ToString("N");
    public List<int> ProductIds { get; set; } = [];
    public List<int> Quantities { get; set; } = [];
}

public sealed record AssistedOrderProductViewModel(int Id, string Sku, string Name, string Unit, decimal UnitPrice, int MinimumCases, bool IsAvailable);

public sealed record AssistedOrderResult(bool Succeeded, int? OrderId, string? ErrorMessage)
{
    public static AssistedOrderResult Failure(string message) => new(false, null, message);
    public static AssistedOrderResult Success(int orderId) => new(true, orderId, null);
}