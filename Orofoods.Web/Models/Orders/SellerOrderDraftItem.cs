using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.Models.Orders;

public class SellerOrderDraftItem
{
    public int Id { get; set; }
    public int SellerOrderDraftId { get; set; }
    public SellerOrderDraft? SellerOrderDraft { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }
    public decimal RequestedDiscountAmount { get; set; }
}
