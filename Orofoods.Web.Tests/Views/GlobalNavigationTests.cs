namespace Orofoods.Web.Tests.Views;

public class GlobalNavigationTests
{
    [Fact]
    public void Shared_layout_places_the_back_control_inside_the_context_bar()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "site.js"));

        Assert.Contains("class=\"page-context-bar\"", layout);
        Assert.Contains("id=\"siteBackButton\"", layout);
        Assert.Contains("data-fallback-url=\"/\"", layout);
        Assert.Contains("siteBackButton", script);
    }

    [Fact]
    public void Shared_layout_and_header_use_correct_portuguese_accentuation()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Contains("In&#237;cio", header);
        Assert.Contains("Sobre n&#243;s", header);
        Assert.Contains("Navega&#231;&#227;o segura", layout);
    }

    [Fact]
    public void Shared_layout_loads_local_font_awesome_and_uses_icons_for_navigation()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("~/lib/fontawesome/css/all.min.css", layout);
        Assert.Contains("fa-arrow-left", layout);
    }

    [Fact]
    public void Shared_layout_uses_a_compact_structured_footer()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("site-footer-grid", layout);
        Assert.Contains("footer-brand", layout);
        Assert.Contains("footer-legal", layout);
        Assert.Contains("fa-building", layout);
    }

    [Fact]
    public void Shared_header_targets_public_home_routes()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var header = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_SiteHeader.cshtml"));

        Assert.Equal(6, header.Split("asp-area=\"\" asp-controller=\"Home\"").Length - 1);
    }
}
