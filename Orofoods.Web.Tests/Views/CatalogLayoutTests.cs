namespace Orofoods.Web.Tests.Views;

public class CatalogLayoutTests
{
    [Fact]
    public void Catalog_filters_keep_search_category_brand_and_button_on_one_desktop_row()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web/wwwroot/css/site.css"));
        var styles = File.ReadAllText(path);

        Assert.Contains("grid-template-columns:minmax(0,1fr) 290px 215px 150px", styles);
    }
}
