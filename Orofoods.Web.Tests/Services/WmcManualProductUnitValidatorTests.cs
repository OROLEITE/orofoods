using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Services;

public sealed class WmcManualProductUnitValidatorTests
{
    [Fact]
    public void Accepts_a_non_pc_primary_unit_and_a_cx_master()
    {
        var result = WmcManualProductUnitValidation.Validate(
            Source("UN", "CX"),
            [
                new("UN", "UNIDADE", 1m, "7899679701348", false),
                new("CX", "CAIXA", 24m, "17899679701345", true)
            ],
            snapshotComplete: true);

        Assert.True(result.IsValid);
        Assert.Equal("UN", result.PrimaryUnitCode);
        Assert.Equal("CX", result.MasterUnitCode);
        Assert.Equal("7899679701348", result.ProposedEan13);
        Assert.Equal("17899679701345", result.ProposedDun14);
    }

    [Fact]
    public void Accepts_pt_primary_and_cp_master_without_a_fixed_unit_list()
    {
        var result = WmcManualProductUnitValidation.Validate(
            Source("PT", "CP"),
            [
                new("PT", "PACOTE", 1m, "7899679701348", false),
                new("CP", "CAIXA", 12m, "17899679701345", true)
            ],
            snapshotComplete: true);

        Assert.True(result.IsValid);
        Assert.Equal("PT", result.PrimaryUnitCode);
        Assert.Equal("CP", result.MasterUnitCode);
    }

    [Fact]
    public void Accepts_a_primary_unit_when_wmc_does_not_mark_a_master()
    {
        var result = WmcManualProductUnitValidation.Validate(
            Source("UN", alternate: null, packageQuantity: null, conversionQuantity: null),
            [new("UN", "UNIDADE", 1m, "7899679701348", false)],
            snapshotComplete: true);

        Assert.True(result.IsValid);
        Assert.Equal("UN", result.PrimaryUnitCode);
        Assert.Null(result.MasterUnitCode);
        Assert.Null(result.ProposedDun14);
    }

    [Fact]
    public void Explains_duplicate_master_units()
    {
        var result = WmcManualProductUnitValidation.Validate(
            Source("UN", "CX"),
            [
                new("UN", "UNIDADE", 1m, "7899679701348", false),
                new("CX", "CAIXA", 24m, "17899679701345", true),
                new("CP", "CAIXA", 24m, "17899679701345", true)
            ],
            snapshotComplete: true);

        Assert.False(result.IsValid);
        Assert.Contains("mais de uma unidade master", result.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Explains_invalid_factor_and_barcode_without_inventing_values()
    {
        var result = WmcManualProductUnitValidation.Validate(
            Source("UN", "CX"),
            [
                new("UN", "UNIDADE", 0m, "nao-e-gtin", false),
                new("CX", "CAIXA", 24m, "17899679701345", true)
            ],
            snapshotComplete: true);

        Assert.False(result.IsValid);
        Assert.Contains("fator", result.BlockReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("código de barras", result.BlockReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ProposedEan);
        Assert.Null(result.ProposedEan13);
    }

    [Fact]
    public void Rejects_non_positive_wmc_quantities()
    {
        var result = WmcManualProductUnitValidation.Validate(
            Source("UN", "CX", packageQuantity: 0, conversionQuantity: -1),
            [
                new("UN", "UNIDADE", 1m, "7899679701348", false),
                new("CX", "CAIXA", 24m, "17899679701345", true)
            ],
            snapshotComplete: true);

        Assert.False(result.IsValid);
        Assert.Contains("QTDE_EMB", result.BlockReason, StringComparison.Ordinal);
        Assert.Contains("QTDE_CONV", result.BlockReason, StringComparison.Ordinal);
    }

    private static WmcProductRecord Source(string unit, string? alternate, decimal? packageQuantity = 24, decimal? conversionQuantity = 24) =>
        new("P", "Produto", "A", unit, 5, 5, Ean: "7899679701348", UnAltern: alternate,
            QtdeEmbalagem: packageQuantity, QtdeConversao: conversionQuantity, PrecoVenda: 10);
}
