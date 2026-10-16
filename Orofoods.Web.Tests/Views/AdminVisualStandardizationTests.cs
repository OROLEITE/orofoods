namespace Orofoods.Web.Tests.Views;

public sealed class AdminVisualStandardizationTests
{
    private static string WebProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Integrations_keeps_status_error_and_operational_actions()
    {
        var view = ReadView("Integrations", "Index.cshtml");
        var controller = Read("Areas", "Admin", "Controllers", "IntegrationsController.cs");

        Assert.Contains("name=\"q\"", view);
        Assert.Contains("name=\"status\"", view);
        Assert.Contains("name=\"wmcStatus\"", view);
        Assert.Contains("erp-status-badge", view);
        Assert.Contains("data-bs-toggle=\"modal\"", view);
        Assert.Contains("data-error=\"@order.IntegrationError\"", view);
        Assert.Contains("asp-action=\"ExportWmc\" method=\"post\"", view);
        Assert.Contains("Authorize(Roles = \"Administrador\")", controller);
        Assert.Contains("WmcManualExportService", controller);
        Assert.Contains("WmcManualExportEligibleOrderIds", controller);
    }

    [Fact]
    public void B2b_customer_list_keeps_filters_status_sorting_and_row_actions()
    {
        var view = ReadView("Customers", "Index.cshtml");

        Assert.Contains("name=\"q\"", view);
        Assert.Contains("name=\"status\"", view);
        Assert.Contains("asp-route-sort=\"customer\"", view);
        Assert.Contains("asp-route-page=\"@currentPage\"", view);
        Assert.Contains("customer-status--@customer.Status", view);
        Assert.Contains("value=\"@customerStatus\"", view);
        Assert.Contains("StatusLabel(customerStatus)", view);
        foreach (var statusMapping in new[]
        {
            "CustomerStatus.Pending => \"Pendente\"",
            "CustomerStatus.Approved => \"Aprovado\"",
            "CustomerStatus.Blocked => \"Bloqueado\"",
            "_ => \"Inativo\""
        })
            Assert.Contains(statusMapping, view);
        Assert.Contains("gerencie as condições comerciais", view);
        Assert.DoesNotContain("condi&ccedil;&otilde;es", view);
        Assert.Contains("asp-action=\"Details\"", view);
        Assert.Contains("asp-action=\"Edit\"", view);
        Assert.Contains("asp-action=\"NewOrder\"", view);
        Assert.Contains("admin-mobile-table", view);
    }

    [Fact]
    public void Inventory_keeps_adjustment_and_wmc_controls()
    {
        var view = ReadView("Inventory", "Index.cshtml");

        Assert.Contains("asp-action=\"Adjust\" method=\"post\"", view);
        Assert.Contains("name=\"quantityOnHand\"", view);
        Assert.Contains("name=\"reason\"", view);
        Assert.Contains("asp-action=\"ResumeWmcStockSync\" method=\"post\"", view);
        Assert.Contains("asp-action=\"ReleaseWmcInitialLoad\" method=\"post\"", view);
        Assert.Contains("WmcInitialLoadReady", view);
        Assert.Contains("WmcCode == \"243680\"", view);
    }

    [Fact]
    public void Seller_edit_keeps_bound_fields_and_save_cancel_actions()
    {
        var view = ReadView("SalesRepresentatives", "Edit.cshtml");

        Assert.Contains("admin-seller-edit-page", view);
        foreach (var field in new[] { "asp-for=\"Id\"", "asp-for=\"Name\"", "asp-for=\"Email\"", "asp-for=\"Phone\"", "asp-for=\"Region\"", "asp-for=\"IsActive\"" })
            Assert.Contains(field, view);

        Assert.Contains("method=\"post\"", view);
        Assert.Contains("asp-action=\"Index\"", view);
        Assert.Contains("Salvar vendedor", view);
        Assert.Contains("admin-form-actions", view);
    }

    [Fact]
    public void Seller_edit_actions_keep_compact_touch_targets_at_mobile_widths()
    {
        var registrations = ReadCss("admin-registrations.css");
        var compact = System.Text.RegularExpressions.Regex.Replace(registrations, @"\s+", string.Empty);

        Assert.Contains("align-items:center;", compact);
        Assert.Contains("flex:01auto;", compact);
        Assert.Contains("white-space:nowrap;", compact);
        Assert.Contains("min-height:44px", compact);
        Assert.Contains("@media(max-width:767px)", compact);
    }

    [Fact]
    public void Admin_visual_rules_are_scoped_compact_and_responsive()
    {
        var integrations = ReadCss("admin-integrations.css");
        var customers = ReadCss("admin-customers.css");
        var shell = ReadCss("admin-shell.css");
        var registrations = ReadCss("admin-registrations.css");
        var actions = ReadCss("primary-action-buttons.css");

        Assert.Contains("body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-integrations-page .erp-filter-card", integrations);
        Assert.Contains("body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .customer-list-page .customer-list-tools", customers);
        Assert.Contains("body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-inventory-page .admin-inventory-adjustment", registrations);
        Assert.Contains("body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .admin-seller-edit-page .admin-form-actions", registrations);
        Assert.Contains("overflow-wrap: anywhere", integrations);
        Assert.Contains("min-height: 44px", registrations);
        Assert.Contains(".admin-authenticated .admin-inventory-page", shell);
        Assert.Matches(@"@media\s*\(max-width:\s*767px\)", integrations);
        Assert.Matches(@"@media\s*\(max-width:\s*767px\)", customers);
        Assert.Matches(@"@media\s*\(max-width:\s*767px\)", registrations);
        Assert.Contains("#0E7490", actions);
        Assert.Contains("#FFFFFF", actions);
    }

    [Fact]
    public void Admin_operational_primary_actions_keep_white_aa_contrast_and_visible_focus()
    {
        var actions = ReadCss("primary-action-buttons.css");

        Assert.Contains(".admin-authenticated .admin-integrations-page .erp-row-actions .btn-erp-export", actions);
        Assert.Contains(".admin-authenticated .admin-inventory-page .admin-inventory-adjustment .admin-edit-action", actions);
        Assert.Contains("outline: 3px solid #0E7490", actions);
        Assert.Contains("outline: 3px solid #38BDF8", actions);
        Assert.True(ContrastRatio("#FFFFFF", "#0E7490") >= 4.5, "The normal operational action must meet WCAG AA.");
        Assert.True(ContrastRatio("#FFFFFF", "#155E75") >= 4.5, "The hover and active operational action must meet WCAG AA.");
        Assert.True(ContrastRatio("#0E7490", "#FFFFFF") >= 3, "The light-theme focus ring must remain visible.");
        Assert.True(ContrastRatio("#38BDF8", "#0F172A") >= 3, "The dark-theme focus ring must remain visible.");
    }

    private static string ReadView(string folder, string fileName) =>
        Read("Areas", "Admin", "Views", folder, fileName);

    private static string ReadCss(string fileName) => Read("wwwroot", "css", fileName);

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { WebProjectPath }.Concat(parts).ToArray()));

    private static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    private static double RelativeLuminance(string color)
    {
        static double Linearize(double component) =>
            component <= .04045 ? component / 12.92 : Math.Pow((component + .055) / 1.055, 2.4);

        var red = Linearize(Convert.ToInt32(color.Substring(1, 2), 16) / 255d);
        var green = Linearize(Convert.ToInt32(color.Substring(3, 2), 16) / 255d);
        var blue = Linearize(Convert.ToInt32(color.Substring(5, 2), 16) / 255d);
        return .2126 * red + .7152 * green + .0722 * blue;
    }
}
