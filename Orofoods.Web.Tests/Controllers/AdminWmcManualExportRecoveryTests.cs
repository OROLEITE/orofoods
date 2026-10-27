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
        AssertMethodHas<ValidateAntiForgeryTokenAttribute>(controller, "ConfirmWmcImported");
        AssertMethodHas<ValidateAntiForgeryTokenAttribute>(controller, "RejectWmcTxt");
        AssertMethodHas<ValidateAntiForgeryTokenAttribute>(controller, "RegenerateWmcTxt");
        AssertMethodHas<HttpPostAttribute>(controller, "RejectWmcTxt");
        AssertMethodHas<HttpPostAttribute>(controller, "ConfirmWmcImported");
        AssertMethodHas<HttpPostAttribute>(controller, "RegenerateWmcTxt");
        AssertMethodHas<HttpGetAttribute>(controller, "DownloadWmcTxt");
        Assert.DoesNotContain(typeof(ValidateAntiForgeryTokenAttribute),
            controller.GetMethod("DownloadWmcTxt")!.GetCustomAttributes().Select(attribute => attribute.GetType()));
    }

    [Fact]
    public void Json_import_endpoint_allows_the_three_roles_and_is_an_antiforgery_post()
    {
        var controller = typeof(WmcOrderImportsController);
        Assert.Null(controller.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        var method = controller.GetMethod("ImportToWmc");
        Assert.NotNull(method);
        Assert.Equal("Administrador,Vendedor,Operador", method!.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var reconcile = controller.GetMethod("ReconcileNroPedido");
        Assert.Equal("Administrador,Vendedor,Operador", reconcile?.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.NotNull(reconcile?.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(reconcile?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var index = controller.GetMethod("Index");
        Assert.Equal("Administrador,Vendedor,Operador", index?.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
    }

    private static void AssertMethodHas<TAttribute>(Type controller, string methodName)
        where TAttribute : Attribute =>
        Assert.NotNull(controller.GetMethod(methodName)?.GetCustomAttribute<TAttribute>());
}
