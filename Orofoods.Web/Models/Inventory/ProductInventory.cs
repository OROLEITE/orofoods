namespace Orofoods.Web.Models.Inventory;

public class ProductInventory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public List<InventoryAdjustment> Adjustments { get; set; } = [];

    public int AvailableQuantity => Math.Max(0, QuantityOnHand - QuantityReserved);
}
