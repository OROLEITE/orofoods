using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orofoods.Web.Models.Catalog;

public class Product
{
    public int Id { get; set; }
    [MaxLength(30)] public string Sku { get; set; } = "";
    [MaxLength(30)] public string? WmcCode { get; set; }
    [MaxLength(140)] public string Name { get; set; } = "";
    public int ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }
    [MaxLength(80)] public string Brand { get; set; } = "";
    [MaxLength(500)] public string Description { get; set; } = "";
    public decimal? Weight { get; set; }
    [MaxLength(20)] public string Unit { get; set; } = "caixa";
    public int UnitsPerPackage { get; set; }
    public int UnitsPerCase { get; set; }
    public int MinimumCases { get; set; } = 1;
    public decimal BasePrice { get; set; }
    public decimal? PromotionalPrice { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsPromotional { get; set; }
    [MaxLength(200)] public string StorageInformation { get; set; } = "";
    [MaxLength(60)] public string StorageTemperature { get; set; } = "";
    [MaxLength(80)] public string ApproximateShelfLife { get; set; } = "";
    [MaxLength(400)] public string Ingredients { get; set; } = "";
    [MaxLength(400)] public string AdditionalInformation { get; set; } = "";
    public int? SubstituteProductId { get; set; }
    public Product? SubstituteProduct { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProductImage> Images { get; set; } = [];
    [MaxLength(12)] public string Accent { get; set; } = "#d99624";

    [NotMapped]
    public string Category => ProductCategory?.Name ?? "";

    [NotMapped]
    public string UnitDescription => UnitsPerCase > 0
        ? $"Caixa com {UnitsPerCase} unidades"
        : Unit;

    [NotMapped]
    public bool IsCommerciallyAvailable { get; set; }
}
