namespace Orofoods.Web.Models.Orders;

public class SavedOrderItem
{
    public int Id { get; set; }
    public int SavedOrderId { get; set; }
    public SavedOrder? SavedOrder { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}
