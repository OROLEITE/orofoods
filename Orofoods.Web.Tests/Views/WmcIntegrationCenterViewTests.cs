namespace Orofoods.Web.Tests.Views;

public sealed class WmcIntegrationCenterViewTests
{
    [Fact]
    public void Wmc_view_defines_the_four_tab_integration_center_and_real_overview_hooks()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Integrations", "Wmc.cshtml");

        Assert.Contains("Visão Geral", view, StringComparison.Ordinal);
        Assert.Contains("Clientes", view, StringComparison.Ordinal);
        Assert.Contains("Produtos", view, StringComparison.Ordinal);
        Assert.Contains("Diagnóstico", view, StringComparison.Ordinal);
        Assert.Contains("role=\"tablist\"", view, StringComparison.Ordinal);
        Assert.Contains("role=\"tab\"", view, StringComparison.Ordinal);
        Assert.Contains("role=\"tabpanel\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-overview-connection", view, StringComparison.Ordinal);
        Assert.Contains("WmcBrandQueryState", view, StringComparison.Ordinal);
        Assert.Contains("LastRun", view, StringComparison.Ordinal);
        Assert.Contains("IsRunning", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-initial-tab", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Lorem ipsum", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wmc_view_preserves_manual_actions_and_read_only_diagnostics()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Integrations", "Wmc.cshtml");

        foreach (var action in new[]
        {
            "WmcCustomerPreview",
            "WmcCustomerImport",
            "WmcCustomerRegularizationPreview",
            "WmcCustomerRegularization",
            "WmcProducts",
            "WmcConfirmSelection",
            "WmcImportProducts",
            "WmcPreview"
        })
        {
            Assert.Contains($"asp-action=\"{action}\"", view, StringComparison.Ordinal);
        }

        Assert.Contains("@Html.AntiForgeryToken()", view, StringComparison.Ordinal);
        Assert.Contains("@await Component.InvokeAsync(\"WmcActivationDiagnostics\")", view, StringComparison.Ordinal);
        Assert.Contains("simulationValidated", view, StringComparison.Ordinal);
        Assert.Contains("reimportExisting", view, StringComparison.Ordinal);
        Assert.Contains("selectedProductCodes", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Wmc_center_declares_client_loading_duplicate_submit_and_dark_responsive_contracts()
    {
        var view = ReadWebFile("Areas", "Admin", "Views", "Integrations", "Wmc.cshtml");
        var script = ReadWebFile("wwwroot", "js", "wmc-integration-center.js");
        var css = ReadWebFile("wwwroot", "css", "admin-integrations.css");

        Assert.Contains("wmc-integration-center.js", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-submit-once", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-loading", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-search", script, StringComparison.Ordinal);
        Assert.Contains("aria-selected", script, StringComparison.Ordinal);
        Assert.Contains("#0E7490", css, StringComparison.Ordinal);
        Assert.Contains("#0B5D75", css, StringComparison.Ordinal);
        Assert.Contains("--admin-dark-surface", css, StringComparison.Ordinal);
        Assert.Contains("@media", css, StringComparison.Ordinal);
        Assert.Contains("wmc-tab", css, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }
}
