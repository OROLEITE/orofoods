namespace Orofoods.Web.Tests.Views;

public class AdminFooterVisualTests
{
    [Fact]
    public void Footer_compaction_is_scoped_to_admin_layout_and_preserves_responsive_layout_and_border()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var adminLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));
        var sharedLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var portalLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_PortalLayout.cshtml"));
        var css = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-footer.css"));
        var sharedCss = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("admin-footer.css", adminLayout);
        Assert.DoesNotContain("admin-footer.css", sharedLayout);
        Assert.DoesNotContain("admin-footer.css", portalLayout);
        Assert.Contains("body.admin-authenticated > .site-footer", css);
        Assert.Contains("padding: 8px 0;", css);
        Assert.Contains("border-top: 1px solid #343B33;", css);
        Assert.Contains("align-items: center;", css);
        Assert.Contains("grid-template-columns: 1fr;", css);
        Assert.Contains("width: 42px;", css);
        Assert.DoesNotContain("height: 100px;", css);
        Assert.Contains("grid-template-columns:minmax(220px,1.15fr) auto minmax(300px,1.35fr)", sharedCss);
        Assert.Contains("@media(max-width:600px)", sharedCss);
    }
}
