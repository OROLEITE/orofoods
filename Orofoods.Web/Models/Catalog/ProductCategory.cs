using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Catalog;

public class ProductCategory
{
    public int Id { get; set; }
    [MaxLength(80)] public string Name { get; set; } = "";
    [MaxLength(120)] public string Slug { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}
