using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Inventory;

public class InventoryAdjustment
{
    public int Id { get; set; }
    public int ProductInventoryId { get; set; }
    public ProductInventory? ProductInventory { get; set; }
    public int PreviousQuantityOnHand { get; set; }
    public int NewQuantityOnHand { get; set; }
    [MaxLength(300)] public string Reason { get; set; } = "";
    [MaxLength(450)] public string? AdjustedByUserId { get; set; }
    public DateTime AdjustedAt { get; set; } = DateTime.UtcNow;
}
