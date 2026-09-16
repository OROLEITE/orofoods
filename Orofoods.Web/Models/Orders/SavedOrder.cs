using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Orders;

public class SavedOrder
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<SavedOrderItem> Items { get; set; } = [];
}
