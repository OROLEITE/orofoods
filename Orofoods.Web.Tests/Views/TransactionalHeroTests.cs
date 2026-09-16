namespace Orofoods.Web.Tests.Views;

public class TransactionalHeroTests
{
    [Theory]
    [InlineData("Cart.cshtml")]
    [InlineData("Checkout.cshtml")]
    public void Transactional_views_use_the_compact_hero(string viewName)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var markup = File.ReadAllText(Path.Combine(projectPath, "Views", "Portal", viewName));

        Assert.Contains("page-hero page-hero--compact", markup);
    }
}
