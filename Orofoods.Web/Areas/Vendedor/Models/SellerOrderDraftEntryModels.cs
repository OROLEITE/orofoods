using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Sellers;

namespace Orofoods.Web.Areas.Vendedor.Models;

public sealed class SellerOrderDraftCreateForm
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    public Guid CreateRequestKey { get; set; }
    public int? DeliveryAddressId { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<SellerOrderDraftFormItem> Items { get; set; } = [];
}

public sealed class SellerOrderDraftSaveForm
{
    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    public int? DeliveryAddressId { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public List<SellerOrderDraftFormItem> Items { get; set; } = [];
    public List<int> RemovedProductIds { get; set; } = [];
}

public sealed class SellerOrderDraftSubmitForm
{
    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [Required]
    [StringLength(262144, MinimumLength = 1)]
    public string QuoteToken { get; set; } = "";
}

public sealed class SellerOrderDraftFormItem
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public sealed class SellerOrderDraftEditorViewModel
{
    public int? DraftId { get; init; }
    public SellerOrderDraftStatus? Status { get; init; }
    public int? SubmittedOrderId { get; init; }
    public string? SubmittedOrderNumber { get; init; }
    public string? QuoteToken { get; init; }
    public Guid CreateRequestKey { get; init; }
    public int ExpectedVersion { get; init; }
    public bool IsPersisted => DraftId.HasValue;
    public bool CanEdit => !IsPersisted || Status == SellerOrderDraftStatus.Draft;
    public int CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string CustomerCode { get; init; }
    public string? CustomerSearch { get; init; }
    public IReadOnlyList<SellerCustomerRow> CustomerResults { get; init; } = [];
    public int CustomerPage { get; init; } = 1;
    public int CustomerPageSize { get; init; } = 20;
    public int CustomerTotalCount { get; init; }
    public int CustomerTotalPages { get; init; }
    public string? ProductSearchUrl { get; init; }
    public IReadOnlyList<SellerDraftAddressOption> Addresses { get; init; } = [];
    public IReadOnlyList<SellerDraftPaymentOption> PaymentTerms { get; init; } = [];
    public int? DeliveryAddressId { get; init; }
    public int? PaymentTermId { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public decimal MinimumOrder { get; init; } = 160m;
    public decimal RemainingForMinimum => Math.Max(0m, MinimumOrder - Total);
    public string Notes { get; init; } = "";
    public IReadOnlyList<SellerOrderDraftEditorItem> Items { get; init; } = [];
    public IReadOnlyList<int> RemovedProductIds { get; init; } = [];
    public decimal Total => Items.Where(item => !item.RemovalPending).Sum(item => item.Subtotal);
}

public sealed record SellerDraftAddressOption(int Id, string Label, string FullAddress);

public sealed record SellerDraftPaymentOption(int Id, string Code, string Name, int DaysUntilDue);

public sealed record SellerOrderDraftEditorItem(
    int ProductId,
    string Sku,
    string Name,
    string UnitDescription,
    int Quantity,
    int MinimumCases,
    decimal UnitPrice,
    bool RemovalPending = false,
    string PriceUnitLabel = "caixa")
{
    public decimal Subtotal => UnitPrice * Quantity;
}

public sealed record SellerOrderDraftListViewModel(IReadOnlyList<SellerOrderDraftView> Drafts);
