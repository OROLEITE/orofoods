using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Catalog;

public sealed class WmcProductUnit
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [MaxLength(20)]
    public string UnitCode { get; set; } = "";

    [MaxLength(120)]
    public string Description { get; set; } = "";

    public decimal? Factor { get; set; }

    [MaxLength(80)]
    public string? Barcode { get; set; }

    public bool IsMaster { get; set; }
}
