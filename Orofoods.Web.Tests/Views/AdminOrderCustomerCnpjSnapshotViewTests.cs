using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Orofoods.Web.Areas.Admin.Controllers;

namespace Orofoods.Web.Tests.Views;

public sealed class AdminOrderCustomerCnpjSnapshotViewTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Order_details_exposes_audited_snapshot_correction_with_admin_only_post()
    {
        var view = File.ReadAllText(Path.Combine(ProjectPath, "Areas/Admin/Views/Orders/Details.cshtml"));
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.CorrectCustomerCnpjSnapshot));

        Assert.Contains("data-customer-cnpj-snapshot", view);
        Assert.Contains("name=\"cnpj\"", view);
        Assert.Contains("name=\"reason\"", view);
        Assert.Contains("maxlength=\"1000\"", view);
        Assert.Contains("asp-action=\"CorrectCustomerCnpjSnapshot\"", view);
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Roles == "Administrador");
        Assert.Contains(method.GetCustomAttributes(), attribute => attribute is Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute);
    }
}
