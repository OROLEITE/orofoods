namespace Orofoods.Web.Tests.Views;

public class SellerPortalShellTests
{
    private static string ProjectRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Seller_area_uses_its_own_shell_layout()
    {
        var viewStart = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_ViewStart.cshtml"));

        Assert.Contains("_SellerLayout.cshtml", viewStart);
    }

    [Fact]
    public void Seller_shell_exposes_an_accessible_mobile_offcanvas_and_skip_link()
    {
        var layoutPath = Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "_SellerLayout.cshtml");

        Assert.True(File.Exists(layoutPath), "The seller area needs its own shell layout.");
        var layout = File.ReadAllText(layoutPath);

        Assert.Contains("offcanvas-md", layout);
        Assert.Contains("aria-controls=\"sellerSidebar\"", layout);
        Assert.Contains("data-bs-target=\"#sellerSidebar\"", layout);
        Assert.Contains("href=\"#sellerMain\"", layout);
        Assert.Contains("aria-label=\"Navegação do vendedor\"", layout);
    }

    [Fact]
    public void Seller_shell_styles_support_mobile_tablet_desktop_and_dark_theme_focus()
    {
        var styles = File.ReadAllText(Path.Combine(ProjectRoot, "wwwroot", "css", "seller-area.css"));

        Assert.Contains("@media (max-width: 767.98px)", styles);
        Assert.Contains("@media (min-width: 768px)", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.seller-app-page", styles);
        Assert.Contains("#38BDF8", styles);
        Assert.Contains(":focus-visible", styles);
        Assert.Contains("min-height: 44px", styles);
    }

    [Fact]
    public void Seller_dashboard_uses_compact_commercial_sections_without_changing_its_data_contract()
    {
        var dashboard = File.ReadAllText(Path.Combine(ProjectRoot, "Areas", "Vendedor", "Views", "Dashboard", "Index.cshtml"));

        Assert.Contains("class=\"seller-dashboard\"", dashboard);
        Assert.Contains("Model.ActiveCustomerCount", dashboard);
        Assert.Contains("asp-controller=\"Customers\"", dashboard);
        Assert.Contains("asp-controller=\"OrderDrafts\"", dashboard);
    }
}
