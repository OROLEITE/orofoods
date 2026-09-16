namespace Orofoods.Web.Models.Pricing;

public class PriceTableItem
{
    public int Id { get; set; }
    public int PriceTableId { get; set; }
    public PriceTable? PriceTable { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Price { get; set; }
    public decimal? PromotionalPrice { get; set; }
}
