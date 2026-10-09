namespace Orofoods.Web.Tests.Views;

public sealed class WmcProductPreviewViewTests
{
    [Fact]
    public void Wmc_preview_is_admin_antiforgery_protected_and_separate_from_sync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "admin-integrations.css"));

        Assert.Contains("Authorize(Roles = \"Administrador\")", controller, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> WmcPreview", controller, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"WmcPreview\"", view, StringComparison.Ordinal);
        Assert.Contains("WmcProductPreviewService", controller, StringComparison.Ordinal);
        Assert.Contains("logger.LogError(ex, \"WMC brand loading failed. Operation={Operation}\", \"LoadBrands\")", controller, StringComparison.Ordinal);
        Assert.Contains("if (!wmcSyncOptions.Value.Enabled)", controller, StringComparison.Ordinal);
        Assert.Contains("return Forbid();", controller, StringComparison.Ordinal);
        Assert.Contains("short[]? brandCodes", controller, StringComparison.Ordinal);
        Assert.Contains("name=\"brandCodes\"", view, StringComparison.Ordinal);
        Assert.Contains("Selecionar filtradas", view, StringComparison.Ordinal);
        Assert.Contains("Limpar sele&ccedil;&atilde;o", view, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"WmcSyncNow\"", view, StringComparison.Ordinal);
        Assert.Contains("wmc-preview-card", view, StringComparison.Ordinal);
        Assert.Contains("Novos ativos", view, StringComparison.Ordinal);
        Assert.Contains("Existentes", view, StringComparison.Ordinal);
        Assert.Contains("Ignorados", view, StringComparison.Ordinal);
        Assert.Contains("Conflitos", view, StringComparison.Ordinal);
        Assert.Contains("Detalhes da simula&ccedil;&atilde;o", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-grid", view, StringComparison.Ordinal);
        Assert.Contains("btn-primary", view, StringComparison.Ordinal);
        Assert.DoesNotContain("CÃƒ", view, StringComparison.Ordinal);
        Assert.DoesNotContain("ÃƒÂ§", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-filter", view, StringComparison.Ordinal);
        Assert.Contains("data-product-status=\"@product.Status\"", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-status--@product.Status.ToLowerInvariant()", view, StringComparison.Ordinal);
        Assert.Contains("@product.Status", view, StringComparison.Ordinal);
        Assert.Contains("Todos", view, StringComparison.Ordinal);
        Assert.Contains("wmc-page-header", view, StringComparison.Ordinal);
        Assert.Contains("wmc-hero-card", view, StringComparison.Ordinal);
        Assert.Contains("data-icon=\"refresh-cw\"", view, StringComparison.Ordinal);
        Assert.Contains("Integra&ccedil;&atilde;o Firebird", view, StringComparison.Ordinal);
        Assert.Contains("wmc-status-badge", view, StringComparison.Ordinal);
        Assert.Contains("wmc-last-sync", view, StringComparison.Ordinal);
        Assert.Contains("data-icon=\"package\"", view, StringComparison.Ordinal);
        Assert.Contains(".admin-authenticated .admin-wmc-page{padding-top:28px}", css, StringComparison.Ordinal);
        Assert.Contains(".admin-authenticated .admin-wmc-page .wmc-page-header{margin-bottom:8px}", css, StringComparison.Ordinal);
        Assert.Contains(".admin-authenticated .admin-wmc-page .wmc-brand-grid{display:grid;grid-template-columns:repeat(2", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Wmc_brand_grid_uses_admin_dark_tokens_for_rows_selection_and_scrollbar()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "admin-integrations.css"));

        Assert.Contains(".wmc-brand-row:has", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-brand-grid::-webkit-scrollbar-thumb", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-brand-grid input[type=checkbox]", css, StringComparison.Ordinal);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-wmc-page .wmc-brand-grid", css, StringComparison.Ordinal);
        Assert.Contains("var(--admin-dark-surface)", css, StringComparison.Ordinal);
        Assert.Contains("var(--admin-dark-surface-secondary)", css, StringComparison.Ordinal);
        Assert.Contains("var(--admin-dark-text)", css, StringComparison.Ordinal);
        Assert.Contains("var(--admin-dark-muted)", css, StringComparison.Ordinal);
        Assert.Contains("var(--admin-dark-border)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Wmc_preview_section_uses_compact_spacing_and_keeps_brand_count_colors()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "admin-integrations.css"));

        Assert.Contains(".admin-wmc-page section[aria-labelledby=\"wmc-preview-title\"]", css, StringComparison.Ordinal);
        Assert.Contains("margin-top: 1.5rem", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-preview-heading + p", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-brand-toolbar{", css, StringComparison.Ordinal);
        Assert.Contains("margin:6px 0 8px", css, StringComparison.Ordinal);
        Assert.Contains("font-size:11.5px", css, StringComparison.Ordinal);
        Assert.Contains("font-weight:600", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-brand-count{color:#687166;font-size:11.5px;font-weight:600;white-space:nowrap}", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--muted)", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--admin-dark-muted)", css, StringComparison.Ordinal);
    }
}
