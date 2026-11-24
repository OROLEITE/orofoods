using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Views;

public sealed class AdminWmcProductTableViewTests
{
    private static string WebProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Product_preview_keeps_only_compact_operational_columns_in_the_main_table()
    {
        var view = ReadView();
        var header = Regex.Match(view, "<table class=\"wmc-import-table\">\\s*<caption.*?</caption>\\s*<thead><tr>(.*?)</tr></thead>", RegexOptions.Singleline);

        Assert.True(header.Success, "The WMC product table header contract should be present.");
        Assert.Equal(
            [
                "selection", "wmc-code", "product", "unit", "situation", "status", "wmc-price", "orofoods-price", "details"
            ],
            Regex.Matches(header.Groups[1].Value, "data-wmc-column=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value)
                .ToArray());

        Assert.DoesNotContain("data-wmc-column=\"ean\"", header.Groups[1].Value);
        Assert.DoesNotContain("data-wmc-column=\"dun\"", header.Groups[1].Value);
        Assert.DoesNotContain("data-wmc-column=\"blocked-reason\"", header.Groups[1].Value);
        Assert.Contains("wmc-row-details", view);
        Assert.Contains("@(item.Ean", view);
        Assert.Contains("@(masterUnit?.Barcode", view);
        Assert.Contains("@(item.BlockReason", view);
    }

    [Fact]
    public void Product_preview_has_a_scoped_overflow_contract_for_prices_descriptions_and_mobile_widths()
    {
        var styles = ReadCss();

        Assert.Contains(".wmc-product-table-scroll {", styles);
        Assert.Contains("overflow-x: auto;", styles);
        Assert.Contains(".wmc-import-table {", styles);
        Assert.Contains("table-layout: fixed;", styles);
        Assert.Contains(".wmc-description-cell", styles);
        Assert.Contains("overflow-wrap: anywhere;", styles);
        Assert.Contains(".wmc-price-cell", styles);
        Assert.Contains("white-space: nowrap;", styles);
        Assert.Contains("@media (max-width: 700px)", styles);
    }

    [Fact]
    public void Product_preview_keeps_blocked_products_disabled_and_selection_behavior_intact()
    {
        var view = ReadView();

        Assert.Contains("@(item.Selectable ? null : \"disabled\")", view);
        Assert.Contains("data-wmc-product-checkbox", view);
        Assert.Contains("id=\"wmcProductSearch\"", view);
        Assert.Contains("id=\"wmcProductStatusFilter\"", view);
        Assert.Contains("id=\"wmcProductPreviousPage\"", view);
        Assert.Contains("id=\"wmcProductNextPage\"", view);
    }

    private static string ReadView() =>
        File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

    private static string ReadCss() =>
        File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "wmc-integration-center.css"));
}
