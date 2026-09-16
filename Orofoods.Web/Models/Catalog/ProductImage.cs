using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Catalog;

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [MaxLength(260)] public string Url { get; set; } = "";
    [MaxLength(160)] public string AltText { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}
