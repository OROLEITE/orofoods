using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Views;

public class AdminProductsLightScopeTests
{
    [Fact]
    public void Every_products_light_selector_is_inert_when_the_dark_theme_is_active()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-products-light.css"));
        var selectorRules = Regex.Matches(styles, @"(?<prelude>[^{}]+)\{");
        var selectors = selectorRules
            .Select(match => match.Groups["prelude"].Value.Trim())
            .Where(prelude => !prelude.StartsWith('@'))
            .SelectMany(prelude => prelude.Split(','))
            .Select(selector => selector.Trim())
            .ToArray();

        Assert.NotEmpty(selectors);
        Assert.All(selectors, selector => Assert.StartsWith("html:not([data-theme=\"dark\"])", selector));
        Assert.DoesNotContain("!important", styles, StringComparison.OrdinalIgnoreCase);
    }
}
