namespace Orofoods.Web.Tests.Views;

public class WmcMappingViewTests
{
    [Fact]
    public void Admin_customer_and_product_forms_expose_wmc_mapping_fields()
    {
        var viewsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Admin/Views"));
        var customerEdit = File.ReadAllText(Path.Combine(viewsPath, "Customers", "Edit.cshtml"));
        var productEdit = File.ReadAllText(Path.Combine(viewsPath, "Products", "Edit.cshtml"));
        var salesRepresentativeEdit = File.ReadAllText(Path.Combine(viewsPath, "SalesRepresentatives", "Edit.cshtml"));
        var salesRepresentativeIndex = File.ReadAllText(Path.Combine(viewsPath, "SalesRepresentatives", "Index.cshtml"));

        Assert.Contains("asp-for=\"WmcCode\"", customerEdit);
        Assert.Contains("asp-for=\"WmcCode\"", productEdit);
        Assert.Contains("<form method=\"post\"", salesRepresentativeEdit);
        Assert.Contains("asp-for=\"WmcCode\"", salesRepresentativeEdit);
        Assert.Contains("asp-validation-summary=\"All\"", salesRepresentativeEdit);
        Assert.Contains("asp-validation-for=\"WmcCode\"", salesRepresentativeEdit);
        Assert.Contains("TempData[\"SalesRepresentativeMessage\"]", salesRepresentativeIndex);
    }
}
