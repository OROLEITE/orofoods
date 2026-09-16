using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Pricing;

public class PriceTable
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<PriceTableItem> Items { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
}
