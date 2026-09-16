namespace Orofoods.Web.Tests.Views;

public class BrandAreaTerminologyTests
{
    [Fact]
    public void Public_and_internal_views_identify_the_client_and_admin_areas()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var portal = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", "Dashboard.cshtml"));
        var admin = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));

        Assert.Contains("Portal do cliente", layout);
        Assert.DoesNotContain("JPL", layout);
        Assert.Contains("PORTAL DO CLIENTE", portal);
        Assert.Contains("Administra&#231;&#227;o Orofoods", admin);
        Assert.DoesNotContain("JPL", admin);
        Assert.Contains("class=\"btn btn-outline-dark\" asp-action=\"Catalog\"", portal);
    }
}
