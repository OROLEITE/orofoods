namespace Orofoods.Web.Tests.Views;

public sealed class WmcProductPreviewViewTests
{
    [Fact]
    public void Wmc_product_import_exposes_the_three_step_selected_reimport_flow()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

        Assert.Contains("WmcProducts", controller, StringComparison.Ordinal);
        Assert.Contains("WmcConfirmSelection", controller, StringComparison.Ordinal);
        Assert.Contains("WmcImportProducts", controller, StringComparison.Ordinal);
        Assert.Contains("name=\"selectedProductCodes\"", view, StringComparison.Ordinal);
        Assert.Contains("data-product-search", view, StringComparison.Ordinal);
        Assert.DoesNotContain("WmcPreco", view, StringComparison.Ordinal);
        Assert.DoesNotContain("WmcFator", view, StringComparison.Ordinal);
        Assert.Contains("Unidade de refer&#234;ncia", view, StringComparison.Ordinal);
        Assert.Contains("Unidades oficiais WMC", view, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"WmcUnit-", view, StringComparison.Ordinal);
        Assert.Contains("fator @(unit.Factor", view, StringComparison.Ordinal);
        Assert.Contains("EAN/DUN", view, StringComparison.Ordinal);
        Assert.Contains("tabelas de pre&#231;o, imagens", view, StringComparison.Ordinal);
        Assert.Contains("<dialog", view, StringComparison.Ordinal);
        Assert.Contains("reimportExisting", view, StringComparison.Ordinal);
        Assert.Contains("Existem @Model.Confirmation.ExistingProducts produtos", view, StringComparison.Ordinal);
        Assert.Contains("value=\"true\">Sim", view, StringComparison.Ordinal);
        Assert.Contains("value=\"false\">Não, somente novos", view, StringComparison.Ordinal);
        Assert.Contains("value=\"cancel\">Cancelar", view, StringComparison.Ordinal);
        Assert.Contains("<summary>Prévia técnica de leitura</summary>", view, StringComparison.Ordinal);
        Assert.Contains("cancelar", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wmc_preview_is_admin_antiforgery_protected_and_separate_from_sync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

        Assert.Contains("Authorize(Roles = \"Administrador\")", controller, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> WmcPreview", controller, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"WmcPreview\"", view, StringComparison.Ordinal);
        Assert.Contains("WmcProductPreviewService", controller, StringComparison.Ordinal);
        Assert.Contains("short[]? brandCodes", controller, StringComparison.Ordinal);
        Assert.Contains("name=\"brandCodes\"", view, StringComparison.Ordinal);
        Assert.Contains("Selecionar todas as marcas filtradas", view, StringComparison.Ordinal);
        Assert.Contains("Limpar sele\u00e7\u00e3o", view, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"WmcSyncNow\"", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Wmc_cancel_returns_before_reading_or_importing_any_products()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var actionStart = controller.IndexOf("public async Task<IActionResult> WmcImportProducts", StringComparison.Ordinal);
        var actionEnd = controller.IndexOf("public async Task<IActionResult> WmcPreview", actionStart, StringComparison.Ordinal);
        var action = controller[actionStart..actionEnd];

        Assert.True(actionStart >= 0 && actionEnd > actionStart);
        Assert.True(action.IndexOf("reimportExisting, \"cancel\"", StringComparison.Ordinal) < action.IndexOf("wmcBrandReader.GetAllAsync", StringComparison.Ordinal));
        Assert.True(action.IndexOf("reimportExisting, \"cancel\"", StringComparison.Ordinal) < action.IndexOf("wmcProductImportService.ImportAsync", StringComparison.Ordinal));
    }
}
