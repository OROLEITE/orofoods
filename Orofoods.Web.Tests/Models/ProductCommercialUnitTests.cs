using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Catalog;

namespace Orofoods.Web.Tests.Models;

public sealed class ProductCommercialUnitTests
{
    [Fact]
    public void Explicit_package_configuration_keeps_barcodes_as_strings_and_describes_sales_unit()
    {
        var product = new Product
        {
            Ean13 = "4006381333931",
            Dun14 = "17899679702656",
            CommercialUnitCode = "PCT",
            LogisticsUnitCode = "CX",
            PackagesPerCase = 24,
            MinimumCases = 12
        };

        Assert.Equal("4006381333931", product.Ean13);
        Assert.Equal("17899679702656", product.Dun14);
        Assert.Equal("PCT", product.EffectiveSalesUnitCode);
        Assert.Equal("PCT", product.PriceUnitLabel);
        Assert.Equal("PCT · CX com 24 PCT", product.UnitDescription);
        Assert.Equal(1, product.MinimumSalesQuantity);
        Assert.Equal(12, product.MinimumCases);
        Assert.True(product.HasPackageSalesConfiguration);
    }

    [Fact]
    public void Unclassified_legacy_product_keeps_its_existing_unit_without_assuming_package_sales()
    {
        var product = new Product { Unit = "caixa", UnitsPerCase = 12, MinimumCases = 2 };

        Assert.Equal("caixa", product.EffectiveSalesUnitCode);
        Assert.Equal("caixa", product.PriceUnitLabel);
        Assert.Equal("caixa", product.UnitDescription);
        Assert.False(product.HasPackageSalesConfiguration);
    }

    [Theory]
    [InlineData("123", null)]
    [InlineData(null, "1234567890123")]
    [InlineData("123456789012", "12345678901234")]
    public void Invalid_barcode_lengths_are_rejected(string? ean, string? dun)
    {
        var product = new Product { Ean13 = ean, Dun14 = dun };
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(product, new ValidationContext(product), errors, validateAllProperties: true);

        Assert.False(valid);
    }

    [Theory]
    [InlineData("4006381333930", null)]
    [InlineData(null, "10012345678905")]
    public void Invalid_gs1_check_digits_are_rejected(string? ean, string? dun)
    {
        var product = new Product { Ean13 = ean, Dun14 = dun };
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(product, new ValidationContext(product), errors, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(errors, error => error.ErrorMessage!.Contains("verificador", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Dun14_uses_the_correct_gtin14_check_digit_weighting()
    {
        var product = new Product { Dun14 = "17899679702656" };
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(product, new ValidationContext(product), errors, validateAllProperties: true);

        Assert.True(valid, string.Join("; ", errors.Select(error => error.ErrorMessage)));
    }
}
