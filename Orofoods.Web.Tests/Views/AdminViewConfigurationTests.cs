namespace Orofoods.Web.Tests.Views;

public class AdminViewConfigurationTests
{
    [Fact]
    public void Admin_views_use_shared_layout_and_mvc_tag_helpers()
    {
        var viewsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/Areas/Admin/Views"));
        var viewStart = File.ReadAllText(Path.Combine(viewsPath, "_ViewStart.cshtml"));
        var viewImports = File.ReadAllText(Path.Combine(viewsPath, "_ViewImports.cshtml"));

        Assert.Contains("/Views/Shared/_Layout.cshtml", viewStart);
        Assert.Contains("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers", viewImports);
    }
}
