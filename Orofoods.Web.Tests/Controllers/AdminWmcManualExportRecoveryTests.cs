using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Orofoods.Web.Areas.Admin.Controllers;

namespace Orofoods.Web.Tests.Controllers;

public sealed class AdminWmcManualExportRecoveryTests
{
    [Fact]
    public void Manual_export_recovery_endpoints_are_admin_only_and_mutations_require_antiforgery()
    {
        var controller = typeof(OrdersController);
        var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("Administrador", authorize?.Roles);

        AssertMethodHas<ValidateAntiForgeryTokenAttribute>(controller, "SendToWmc");
        AssertMethodHas<ValidateAntiForgeryTokenAttribute>(controller, "RejectWmcTxt");
        AssertMethodHas<ValidateAntiForgeryTokenAttribute>(controller, "RegenerateWmcTxt");
        AssertMethodHas<HttpPostAttribute>(controller, "RejectWmcTxt");
        AssertMethodHas<HttpPostAttribute>(controller, "RegenerateWmcTxt");
        AssertMethodHas<HttpGetAttribute>(controller, "DownloadWmcTxt");
        Assert.DoesNotContain(typeof(ValidateAntiForgeryTokenAttribute),
            controller.GetMethod("DownloadWmcTxt")!.GetCustomAttributes().Select(attribute => attribute.GetType()));
    }

    private static void AssertMethodHas<TAttribute>(Type controller, string methodName)
        where TAttribute : Attribute =>
        Assert.NotNull(controller.GetMethod(methodName)?.GetCustomAttribute<TAttribute>());
}
