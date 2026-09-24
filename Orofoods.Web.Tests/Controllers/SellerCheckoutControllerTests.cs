using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Areas.Vendedor.Controllers;
using Orofoods.Web.Authorization;
using System.Reflection;

namespace Orofoods.Web.Tests.Controllers;

public class SellerCheckoutControllerTests
{
    [Fact]
    public void Checkout_controller_requires_linked_seller_policy()
    {
        Assert.Contains(typeof(CheckoutController).GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Policy == OrofoodsPolicies.LinkedSalesRepresentative);
    }

    [Fact]
    public void Checkout_post_uses_antiforgery()
    {
        var methods = typeof(CheckoutController).GetMethods()
            .Where(method => method.Name == "Index" && method.GetCustomAttributes<HttpPostAttribute>().Any())
            .ToArray();

        Assert.Single(methods);
        Assert.NotEmpty(methods[0].GetCustomAttributes<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void Checkout_date_uses_pt_br_text_binding_instead_of_browser_date_rendering()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var view = File.ReadAllText(Path.Combine(root, "Orofoods.Web", "Areas", "Vendedor", "Views", "Checkout", "Index.cshtml"));

        Assert.Contains("dd/MM/yyyy", view);
        Assert.DoesNotContain("type=\"date\"", view, StringComparison.OrdinalIgnoreCase);
    }
}