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
        Assert.Contains("wmcMultiBrandSearch", view, StringComparison.Ordinal);
        Assert.Contains("wmcMultiBrandSelectionCount", view, StringComparison.Ordinal);
        Assert.Contains("Consultar Produtos WMC", view, StringComparison.Ordinal);
        Assert.Contains("Revisar Produtos Selecionados", view, StringComparison.Ordinal);
        Assert.Contains("Importar Produtos Selecionados", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Conferir alterações", view, StringComparison.Ordinal);
        Assert.Contains("simulationValidated", view, StringComparison.Ordinal);
        Assert.Contains("Preço proposto", view, StringComparison.Ordinal);
        Assert.Contains("PriceProposal", view, StringComparison.Ordinal);
        Assert.Contains("Preço Orofoods atual", view, StringComparison.Ordinal);
        Assert.Contains("Preço WMC proposto", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Preço WMC / será persistido", view, StringComparison.Ordinal);
        Assert.Contains("dados de produtos existentes só serão aplicados", view, StringComparison.Ordinal);
        Assert.Contains("Selecionar todos", view, StringComparison.Ordinal);
        Assert.Contains("Desmarcar todos", view, StringComparison.Ordinal);
        Assert.Contains("wmcProductSelectionCount", view, StringComparison.Ordinal);
        Assert.Contains("name=\"situationFilter\"", view, StringComparison.Ordinal);
        Assert.Contains("option value=\"active\"", view, StringComparison.Ordinal);
        Assert.Contains("option value=\"inactive\"", view, StringComparison.Ordinal);
        Assert.Contains("option value=\"all\"", view, StringComparison.Ordinal);
        Assert.Contains("data-situation-kind", view, StringComparison.Ordinal);
        Assert.Contains(">Ativos<", view, StringComparison.Ordinal);
        Assert.Contains(">Inativos<", view, StringComparison.Ordinal);
        Assert.Contains(">Eleg", view, StringComparison.Ordinal);
        Assert.Contains(">Bloqueados<", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-product-checkbox", view, StringComparison.Ordinal);
        Assert.Contains("wmcProductPreviousPage", view, StringComparison.Ordinal);
        Assert.Contains("wmcProductNextPage", view, StringComparison.Ordinal);
        Assert.Contains("wmcImportConfirmationForm", view, StringComparison.Ordinal);
        Assert.Contains("var refreshedCandidates = await wmcProductImportService.GetCandidatesAsync", controller, StringComparison.Ordinal);
        Assert.Contains("submitted", view, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"WmcProducts\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmc-legacy-preview", view, StringComparison.Ordinal);
        Assert.Contains("wmc-import-table", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"selection\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"wmc-code\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"product\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"unit\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"situation\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"status\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"wmc-price\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"orofoods-price\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"details\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wmc-column=\"ean\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wmc-column=\"dun\"", view, StringComparison.Ordinal);
        Assert.Contains("item.Stock", view, StringComparison.Ordinal);
        Assert.Contains("var statusKind =", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-status--@statusKind", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-status--new", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-status--existing", css, StringComparison.Ordinal);
        Assert.Contains(".wmc-product-status--blocked", css, StringComparison.Ordinal);
        Assert.Contains("wmc-row-details", view, StringComparison.Ordinal);
        Assert.Contains("wmc-product-detail-grid", view, StringComparison.Ordinal);
        Assert.Contains("UnitProposal", view, StringComparison.Ordinal);
        Assert.Contains("EAN atual", view, StringComparison.Ordinal);
        Assert.Contains("DUN proposto", view, StringComparison.Ordinal);
        Assert.Contains("Unidade principal", view, StringComparison.Ordinal);
        Assert.Contains("Unidade master", view, StringComparison.Ordinal);
        Assert.Contains("Fator unidade principal atual", view, StringComparison.Ordinal);
        Assert.Contains("Fator unidade master atual", view, StringComparison.Ordinal);
        Assert.Contains("QTDE_EMB atual", view, StringComparison.Ordinal);
        Assert.Contains("QTDE_CONV proposto", view, StringComparison.Ordinal);
        Assert.Contains("wmc-import-summary", view, StringComparison.Ordinal);
        Assert.Contains("Confirmar importação", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmc-preview-form", view, StringComparison.Ordinal);
        Assert.DoesNotContain("wmcSelectFiltered", view, StringComparison.Ordinal);
        Assert.Contains("name=\"brandCodes\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wmc-product-preview-toggle", view, StringComparison.Ordinal);
        Assert.Contains("Motivo do bloqueio", view, StringComparison.Ordinal);
        Assert.Contains("Produtos consultados", view, StringComparison.Ordinal);
        Assert.Contains("Elegíveis", view, StringComparison.Ordinal);
        Assert.Equal(1, view.Split("data-wmc-requires-selection", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, CountOccurrences(view, "Consultar Produtos WMC"));
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

    private static int CountOccurrences(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    [Fact]
    public void Official_product_preview_separates_WMC_source_from_local_commercial_data()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var importService = File.ReadAllText(Path.Combine(root, "Integrations", "Erp", "Wmc", "WmcProductImportService.cs"));

        Assert.Contains("Dados WMC · Firebird, somente leitura", view, StringComparison.Ordinal);
        Assert.Contains("Cadastro Orofoods · local, somente leitura", view, StringComparison.Ordinal);
        Assert.Contains("PRODUTOS.PRECOVENDA", view, StringComparison.Ordinal);
        Assert.Contains("EAN unidade principal", view, StringComparison.Ordinal);
        Assert.Contains("Código da master", view, StringComparison.Ordinal);
        Assert.Contains("UNMASTER=", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Aplicação de preços bloqueada", view, StringComparison.Ordinal);
        Assert.Contains("Será gravado no preço base", view, StringComparison.Ordinal);
        Assert.Contains("Ver preços recarregados após a gravação", view, StringComparison.Ordinal);
        Assert.Contains("R$ 1,00/PCT é placeholder local de homologação", view, StringComparison.Ordinal);
        Assert.Contains("PrecoVenda: row.PrecoVenda", importService, StringComparison.Ordinal);
        Assert.Contains("LocalBasePrice: product?.BasePrice", importService, StringComparison.Ordinal);
        Assert.Contains("ApplyMappedFields(current, row, unitProposal)", importService, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_stage_restores_server_selection_and_keeps_table_visible()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

        Assert.Contains("var selectedProductKeys = (Model.SelectedProductKeys ?? []).ToHashSet(StringComparer.Ordinal);", view, StringComparison.Ordinal);
        Assert.Contains("var productKey = $\"{item.BrandCode}:{item.WmcCode}\";", view, StringComparison.Ordinal);
        Assert.Contains("@(isSelected ? \"checked\" : null)", view, StringComparison.Ordinal);
        Assert.Contains("item.Selectable ? null : \"disabled\"", view, StringComparison.Ordinal);
        Assert.Contains("SituationKind", view, StringComparison.Ordinal);
        Assert.Contains("Atualizar produtos j\u00e1 cadastrados conforme WMC", view, StringComparison.Ordinal);

        var tableContainer = view
            .Split('\n')
            .Single(line => line.Contains("<div", StringComparison.Ordinal)
                && line.Contains("data-wmc-product-preview-table", StringComparison.Ordinal));
        Assert.DoesNotContain(" hidden", tableContainer, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wmc-product-preview-toggle", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_selection_controls_apply_to_the_filtered_result_and_keep_global_count()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

        Assert.Contains("const filteredCheckboxes = () => filteredRows()", view, StringComparison.Ordinal);
        Assert.Contains("const filteredEligibleCount = filteredCheckboxes().filter", view, StringComparison.Ordinal);
        Assert.Contains("if (count) count.textContent = `${selectedCount} de ${eligibleCount} produtos selecionados`", view, StringComparison.Ordinal);
        Assert.Contains("filteredCheckboxes().filter(input => !input.disabled).forEach", view, StringComparison.Ordinal);
        Assert.Contains("filteredCheckboxes().forEach(input => { input.checked = false; });", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Product_stage_shows_detail_table_by_default_and_disables_empty_review()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

        var tableContainer = view
            .Split('\n')
            .Single(line => line.Contains("<div", StringComparison.Ordinal)
                && line.Contains("data-wmc-product-preview-table", StringComparison.Ordinal));
        Assert.DoesNotContain(" hidden", tableContainer, StringComparison.Ordinal);
        Assert.DoesNotContain("data-wmc-product-preview-toggle", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Visualizar Produtos Consultados", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"selection\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"wmc-code\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"product\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"unit\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"situation\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"status\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"wmc-price\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"orofoods-price\"", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-column=\"details\"", view, StringComparison.Ordinal);
        Assert.Contains("Motivo do bloqueio", view, StringComparison.Ordinal);
        Assert.Contains("const review = document.getElementById('wmcReviewSelection')", view, StringComparison.Ordinal);
        Assert.Contains("if (review) review.disabled = selectedCount === 0", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_requires_explicit_reimport_authorization_and_shows_change_summary()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var controller = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Controllers", "IntegrationsController.cs"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var confirmationCss = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "wmc-integration-center.css"));

        Assert.Contains("string? reimportExisting", controller, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(reimportExisting)", controller, StringComparison.Ordinal);
        Assert.Contains("type=\"checkbox\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"reimportExisting\"", view, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", view, StringComparison.Ordinal);
        Assert.Contains("Atualizar produtos j\u00e1 cadastrados conforme WMC", view, StringComparison.Ordinal);
        Assert.Contains("pre\u00e7os e dados cadastrais autorizados", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-reimport-state", view, StringComparison.Ordinal);
        Assert.Contains("Atualiza\u00e7\u00e3o de existentes desativada", view, StringComparison.Ordinal);
        Assert.Contains("Autoriza\u00e7\u00e3o para existentes ativada", view, StringComparison.Ordinal);
        Assert.Contains("data-wmc-reimport-authorization", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-confirmation-actions .wmc-reimport-choice", confirmationCss, StringComparison.Ordinal);
        Assert.Contains("[data-wmc-reimport-state=\"disabled\"]", confirmationCss, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"radio\" name=\"reimportExisting\"", view, StringComparison.Ordinal);
        Assert.Contains("Existentes com", view, StringComparison.Ordinal);
        Assert.Contains("Existentes sem", view, StringComparison.Ordinal);
        Assert.Contains("Consultados", view, StringComparison.Ordinal);
        Assert.Contains("Model.Confirmation.Consulted", view, StringComparison.Ordinal);
        Assert.Contains("Model.Confirmation.Selected", view, StringComparison.Ordinal);
        Assert.Contains("Model.Confirmation.Blocked", view, StringComparison.Ordinal);
        Assert.Contains("ExistingWithChanges", view, StringComparison.Ordinal);
        Assert.Contains("ExistingWithoutChanges", view, StringComparison.Ordinal);
        Assert.Contains("wmc-confirmation-product", view, StringComparison.Ordinal);
        Assert.Contains("wmc-confirmation-data-grid", view, StringComparison.Ordinal);
        Assert.Contains("wmc-confirmation-unit-list", view, StringComparison.Ordinal);
        Assert.Contains("ProposedPrimaryFactor", view, StringComparison.Ordinal);
        Assert.Contains("ProposedMasterFactor", view, StringComparison.Ordinal);
        Assert.Contains("ProposedEan13", view, StringComparison.Ordinal);
        Assert.Contains("ProposedDun14", view, StringComparison.Ordinal);
        Assert.Contains("ProposedPackageQuantity", view, StringComparison.Ordinal);
        Assert.Contains("ProposedConversionQuantity", view, StringComparison.Ordinal);
        Assert.Contains("proposal.ProposedEan13 ?? proposal.ProposedEan ?? \"—\"", view, StringComparison.Ordinal);
        Assert.Contains("proposal.ProposedDun14 ?? \"—\"", view, StringComparison.Ordinal);
        Assert.Contains("proposal.ProposedPrimaryFactor?.ToString() ?? \"—\"", view, StringComparison.Ordinal);
        Assert.Contains("proposal.ProposedMasterFactor?.ToString() ?? \"—\"", view, StringComparison.Ordinal);
        Assert.Contains("proposal.ProposedPackageQuantity?.ToString() ?? \"—\"", view, StringComparison.Ordinal);
        Assert.Contains("proposal.ProposedConversionQuantity?.ToString() ?? \"—\"", view, StringComparison.Ordinal);
        Assert.Contains("Unidades alternativas", view, StringComparison.Ordinal);
        Assert.Contains("<details class=\"wmc-confirmation-details\" open>", view, StringComparison.Ordinal);
        Assert.Contains(".wmc-confirmation-product", confirmationCss, StringComparison.Ordinal);
        Assert.Contains(".wmc-confirmation-persistence-note", confirmationCss, StringComparison.Ordinal);
        Assert.Contains("var(--wmc-surface-muted)", confirmationCss, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_cards_show_read_only_wmc_local_and_commercial_diagnostics()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        var confirmationCss = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "wmc-integration-center.css"));

        Assert.Contains("Model.ConfirmationPresentations", view, StringComparison.Ordinal);
        Assert.Contains("wmc-confirmation-diagnostic-sections", view, StringComparison.Ordinal);
        Assert.Contains("Dados oficiais WMC", view, StringComparison.Ordinal);
        Assert.Contains("Cadastro Orofoods", view, StringComparison.Ordinal);
        Assert.Contains("Disponibilidade comercial", view, StringComparison.Ordinal);
        Assert.Contains("Estoque WMC", view, StringComparison.Ordinal);
        Assert.Contains("Estoque local", view, StringComparison.Ordinal);
        Assert.Contains("IsWmcActive", view, StringComparison.Ordinal);
        Assert.Contains("IsActive", view, StringComparison.Ordinal);
        Assert.Contains("IsAvailable", view, StringComparison.Ordinal);
        Assert.Contains("WmcInitialLoadReady", view, StringComparison.Ordinal);
        Assert.Contains("Consulta WMC somente leitura", view, StringComparison.Ordinal);
        Assert.Contains("não confirmada", view, StringComparison.Ordinal);
        Assert.Contains("Após a importação manual", view, StringComparison.Ordinal);
        Assert.Contains("Motivos conhecidos", view, StringComparison.Ordinal);
        Assert.Contains("wmc-confirmation-diagnostic-sections", confirmationCss, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 900px)", confirmationCss, StringComparison.Ordinal);
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
