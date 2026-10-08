namespace Orofoods.Web.Tests.Views;

public sealed class WmcProductPreviewViewTests
{
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
        Assert.Contains("if (!wmcSyncOptions.Value.Enabled)", controller, StringComparison.Ordinal);
        Assert.Contains("return Forbid();", controller, StringComparison.Ordinal);
        Assert.Contains("short[]? brandCodes", controller, StringComparison.Ordinal);
        Assert.Contains("name=\"brandCodes\"", view, StringComparison.Ordinal);
        Assert.Contains("Selecionar filtradas", view, StringComparison.Ordinal);
        Assert.Contains("Limpar sele\u00e7\u00e3o", view, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"WmcSyncNow\"", view, StringComparison.Ordinal);
        Assert.Contains("wmc-preview-card", view, StringComparison.Ordinal);
        Assert.Contains("Novos ativos", view, StringComparison.Ordinal);
        Assert.Contains("Existentes", view, StringComparison.Ordinal);
        Assert.Contains("Ignorados", view, StringComparison.Ordinal);
        Assert.Contains("Conflitos", view, StringComparison.Ordinal);
        Assert.Contains("Detalhes da simula&ccedil;&atilde;o", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-grid", view, StringComparison.Ordinal);
        Assert.Contains("btn-primary", view, StringComparison.Ordinal);
    }
}
