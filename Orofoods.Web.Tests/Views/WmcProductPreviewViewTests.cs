namespace Orofoods.Web.Tests.Views;

public sealed class WmcProductPreviewViewTests
{
    [Fact]
    public void Wmc_import_wizard_preserves_actions_and_uses_three_compact_stages()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "admin-integrations.css"));

        Assert.Contains("Authorize(Roles = \"Administrador\")", controller, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> WmcProducts", controller, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> WmcConfirmSelection", controller, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> WmcImportProducts", controller, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> WmcPreview", controller, StringComparison.Ordinal);
        Assert.Contains("private void SetWmcViewState()", controller, StringComparison.Ordinal);
        Assert.Contains("SetWmcViewState();", controller, StringComparison.Ordinal);
        Assert.Contains("[ValidateAntiForgeryToken]", controller, StringComparison.Ordinal);
        Assert.Contains("wmc-stepper", view, StringComparison.Ordinal);
        Assert.Contains("Selecionar marcas", view, StringComparison.Ordinal);
        Assert.Contains("Conferir produtos", view, StringComparison.Ordinal);
        Assert.Contains("Confirmar importação", view, StringComparison.Ordinal);
        Assert.Contains("wmcBrandSearch", view, StringComparison.Ordinal);
        Assert.Contains("wmcBrandSelectionCount", view, StringComparison.Ordinal);
        Assert.Contains("Carregar produtos", view, StringComparison.Ordinal);
        Assert.Contains("Confirmar alterações", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Conferir alterações", view, StringComparison.Ordinal);
        Assert.Contains("simulationValidated", view, StringComparison.Ordinal);
        Assert.Contains("Preço proposto", view, StringComparison.Ordinal);
        Assert.Contains("PriceProposal", view, StringComparison.Ordinal);
        Assert.Contains("Preço anterior", view, StringComparison.Ordinal);
        Assert.Contains("Novo preço WMC", view, StringComparison.Ordinal);
        Assert.Contains("Selecionar todos", view, StringComparison.Ordinal);
        Assert.Contains("Desmarcar todos", view, StringComparison.Ordinal);
        Assert.Contains("wmcProductSelectionCount", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-product-checkbox", view, StringComparison.Ordinal);
        Assert.Contains("wmcProductPreviousPage", view, StringComparison.Ordinal);
        Assert.Contains("wmcProductNextPage", view, StringComparison.Ordinal);
        Assert.Contains("wmcImportConfirmationForm", view, StringComparison.Ordinal);
        Assert.Contains("var refreshedCandidates = await wmcProductImportService.GetCandidatesAsync", controller, StringComparison.Ordinal);
        Assert.Contains("submitted", view, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"WmcPreview\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmc-legacy-preview", view, StringComparison.Ordinal);
        Assert.Contains("wmc-import-table", view, StringComparison.Ordinal);
        Assert.Contains("<th>EAN</th>", view, StringComparison.Ordinal);
        Assert.Contains("<th>DUN</th>", view, StringComparison.Ordinal);
        Assert.Contains("<th>Unidade / fator</th>", view, StringComparison.Ordinal);
        Assert.Contains("item.Stock", view, StringComparison.Ordinal);
        Assert.Contains("var statusKind =", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-status--@statusKind", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-status--new", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-status--existing", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-status--blocked", css, StringComparison.Ordinal);
        Assert.Contains("wmc-row-details", view, StringComparison.Ordinal);
        Assert.Contains("UnitProposal", view, StringComparison.Ordinal);
        Assert.Contains("EAN atual", view, StringComparison.Ordinal);
        Assert.Contains("DUN proposto", view, StringComparison.Ordinal);
        Assert.Contains("Fator PC atual", view, StringComparison.Ordinal);
        Assert.Contains("Fator CX atual", view, StringComparison.Ordinal);
        Assert.Contains("Fator CX proposto", view, StringComparison.Ordinal);
        Assert.Contains("wmc-import-summary", view, StringComparison.Ordinal);
        Assert.Contains("Confirmar importação", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmc-preview-form", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmcSelectFiltered", view, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"brandCodes\"", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-table-scroll", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-price-cell", css, StringComparison.Ordinal);
        Assert.Contains("text-align:right", css, StringComparison.Ordinal);
        Assert.Contains("#0E7490", css, StringComparison.Ordinal);
        Assert.Contains("#0B5D75", css, StringComparison.Ordinal);
        Assert.Contains("#0F172A", css, StringComparison.Ordinal);
        Assert.Contains("#1E293B", css, StringComparison.Ordinal);
        Assert.Contains("#111827", css, StringComparison.Ordinal);
        Assert.Contains("#334155", css, StringComparison.Ordinal);
        Assert.Contains("#E5E7EB", css, StringComparison.Ordinal);
        Assert.Contains("#94A3B8", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-selection-toolbar", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-pagination", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-row-details-content", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-stepper", css, StringComparison.Ordinal);
        Assert.Contains("var(--admin-dark-surface)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Official_product_preview_separates_WMC_source_from_local_commercial_data()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var importService = File.ReadAllText(Path.Combine(root, "Integrations", "Erp", "Wmc", "WmcProductImportService.cs"));

        Assert.Contains("Dados WMC · Firebird, somente leitura", view, StringComparison.Ordinal);
        Assert.Contains("Cadastro Orofoods · local, somente leitura", view, StringComparison.Ordinal);
        Assert.Contains("PRODUTOS.PRECOVENDA", view, StringComparison.Ordinal);
        Assert.Contains("EAN da PC", view, StringComparison.Ordinal);
        Assert.Contains("DUN da CX", view, StringComparison.Ordinal);
        Assert.Contains("UNMASTER=", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Aplicação de preços bloqueada", view, StringComparison.Ordinal);
        Assert.Contains("Será gravado no preço base", view, StringComparison.Ordinal);
        Assert.Contains("Ver preços recarregados após a gravação", view, StringComparison.Ordinal);
        Assert.Contains("R$ 1,00/PCT é placeholder local de homologação", view, StringComparison.Ordinal);
        Assert.Contains("PrecoVenda: row.PrecoVenda", importService, StringComparison.Ordinal);
        Assert.Contains("LocalBasePrice: product?.BasePrice", importService, StringComparison.Ordinal);
        Assert.Contains("ApplyMappedFields(current, row)", importService, StringComparison.Ordinal);
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
        Assert.Contains("margin-top: 0.5rem !important", css, StringComparison.Ordinal);
        Assert.Contains(".admin-authenticated .admin-wmc-page > .container > p.mt-3", css, StringComparison.Ordinal);
        Assert.Contains("margin-bottom: 8px", css, StringComparison.Ordinal);
        Assert.Contains(".admin-authenticated .admin-wmc-page .wmc-preview-heading{display:flex;align-items:center;gap:8px;margin:0 0 2px}", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-preview-heading + p", css, StringComparison.Ordinal);
        Assert.Contains("margin: 0 0 2px", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-brand-toolbar{", css, StringComparison.Ordinal);
        Assert.Contains("margin:2px 0 8px", css, StringComparison.Ordinal);
        Assert.Contains("font-size:11.5px", css, StringComparison.Ordinal);
        Assert.Contains("font-weight:600", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-brand-count{color:#687166;font-size:11.5px;font-weight:600;white-space:nowrap}", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--muted)", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--admin-dark-muted)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_Bimbo_endpoints_remain_compatible_but_the_panel_is_removed_from_the_main_flow()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var releaseService = File.ReadAllText(Path.Combine(root, "Integrations", "Erp", "Wmc", "WmcInitialLoadReadinessReleaseService.cs"));
        var importService = File.ReadAllText(Path.Combine(root, "Integrations", "Erp", "Wmc", "WmcBimboStagingImportService.cs"));
        var productRules = File.ReadAllText(Path.Combine(root, "Integrations", "Erp", "Wmc", "WmcBimboProductRules.cs"));

        Assert.Contains("WmcBimboSimulate", controller, StringComparison.Ordinal);
        Assert.Contains("WmcBimboImport", controller, StringComparison.Ordinal);
        Assert.Contains("WmcBimboActivate", controller, StringComparison.Ordinal);
        Assert.Contains("if (!hostEnvironment.IsStaging()) return Forbid();", controller, StringComparison.Ordinal);
        Assert.Contains("selectedWmcCodes", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("wmcBimboSimulateForm", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Simular importa&ccedil;&atilde;o", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmcBimboImportForm", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Importar selecionados", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmc-bimbo-card", view, StringComparison.Ordinal);
        Assert.Contains("var canActivateBimbo = hostEnvironment.IsStaging() && initialLoadReleaseOptions.Value.Enabled;", controller, StringComparison.Ordinal);
        Assert.Contains("WmcBimboProductRules.ValidateStagingActivationData(product, inventory)", releaseService, StringComparison.Ordinal);
        Assert.Contains("var issues = allowStagingActivation", importService, StringComparison.Ordinal);
        Assert.Contains("? WmcBimboProductRules.ValidateStagingActivationData(product, inventory)", importService, StringComparison.Ordinal);
        Assert.Contains(": WmcBimboProductRules.ValidateCommercialData(product, product.ProductCategory, inventory)", importService, StringComparison.Ordinal);
        Assert.Contains("if (category is null || !category.IsActive)", productRules, StringComparison.Ordinal);
        Assert.Contains("if (!IsValidBarcode(product.Dun14, 14))", productRules, StringComparison.Ordinal);
    }
}
