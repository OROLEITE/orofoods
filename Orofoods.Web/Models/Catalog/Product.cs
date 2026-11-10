using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Orofoods.Web.Models.Catalog;

public class Product
{
    public int Id { get; set; }
    [MaxLength(30)] public string Sku { get; set; } = "";
    [MaxLength(30)] public string? WmcCode { get; set; }
    [MaxLength(80)] public string? Ean { get; set; }
    [MaxLength(13), Gs1Barcode(13)]
    public string? Ean13 { get; set; }
    [MaxLength(14), Gs1Barcode(14)]
    public string? Dun14 { get; set; }
    [MaxLength(3)] public string? CommercialUnitCode { get; set; }
    [MaxLength(3)] public string? LogisticsUnitCode { get; set; }
    [Range(1, int.MaxValue)] public int? PackagesPerCase { get; set; }
    [MaxLength(30)] public string? WmcBrandCode { get; set; }
    [MaxLength(20)] public string? WmcAlternateUnit { get; set; }
    public decimal? WmcPackageQuantity { get; set; }
    public decimal? WmcConversionQuantity { get; set; }
    [MaxLength(140)] public string Name { get; set; } = "";
    public int ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }
    [MaxLength(80)] public string Brand { get; set; } = "";
    [Display(Name = "descrição comercial")]
    [Required(AllowEmptyStrings = true)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [MaxLength(500)] public string Description { get; set; } = "";
    public decimal? Weight { get; set; }
    [MaxLength(20)] public string Unit { get; set; } = "caixa";
    public int UnitsPerPackage { get; set; }
    public int UnitsPerCase { get; set; }
    public int MinimumCases { get; set; } = 1;
    public decimal BasePrice { get; set; }
    public decimal? PromotionalPrice { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsWmcActive { get; set; } = true;
    public bool WmcStockAvailable { get; set; } = true;
    public bool WmcInitialLoadReady { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPromotional { get; set; }
    [MaxLength(200)] public string StorageInformation { get; set; } = "";
    [MaxLength(60)] public string StorageTemperature { get; set; } = "";
    [MaxLength(80)] public string ApproximateShelfLife { get; set; } = "";
    [MaxLength(400)] public string Ingredients { get; set; } = "";
    [MaxLength(400)] public string AdditionalInformation { get; set; } = "";
    public int? SubstituteProductId { get; set; }
    public Product? SubstituteProduct { get; set; }
    public List<WmcProductUnit> WmcUnits { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public List<ProductImage> Images { get; set; } = [];
    [MaxLength(12)] public string Accent { get; set; } = "#d99624";

    [NotMapped]
    public string Category => ProductCategory?.Name ?? "";

    [NotMapped]
    public string EffectiveSalesUnitCode => string.IsNullOrWhiteSpace(CommercialUnitCode) ? Unit : CommercialUnitCode;

    [NotMapped]
    public string PriceUnitLabel => EffectiveSalesUnitCode.Equals("PCT", StringComparison.OrdinalIgnoreCase)
        ? "PCT"
        : Unit;

    [NotMapped]
    public bool HasPackageSalesConfiguration =>
        string.Equals(CommercialUnitCode, "PCT", StringComparison.OrdinalIgnoreCase)
        && string.Equals(LogisticsUnitCode, "CX", StringComparison.OrdinalIgnoreCase)
        && PackagesPerCase is > 0;

    [NotMapped]
    public int MinimumSalesQuantity => string.Equals(CommercialUnitCode, "PCT", StringComparison.OrdinalIgnoreCase)
        ? 1
        : Math.Max(1, MinimumCases);

    [NotMapped]
    public string UnitDescription => !string.IsNullOrWhiteSpace(CommercialUnitCode)
        ? PackagesPerCase is > 0 ? $"{CommercialUnitCode} · CX com {PackagesPerCase} {CommercialUnitCode}" : CommercialUnitCode
        : Unit;

    [NotMapped]
    public bool IsCommerciallyAvailable { get; set; }
}
