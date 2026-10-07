namespace Orofoods.Web.Tests.Views;

public class AdminPrimaryActionTests
{
    private static string WebProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Primary_action_tokens_define_the_required_palette_and_states()
    {
        var styles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "primary-action-buttons.css"));
        var tokens = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "site.css"));
        var registrationStyles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "admin-registrations.css"));

        Assert.Contains("--action-primary: #2563EB;", tokens);
        Assert.Contains("--action-primary-hover: #1D4ED8;", tokens);
        Assert.Contains("--action-primary-active: #1E40AF;", tokens);
        Assert.Contains("--action-primary-soft: #EFF6FF;", tokens);
        Assert.Contains("--action-primary-foreground: #FFFFFF;", tokens);
        Assert.Contains("--action-primary-disabled-bg: #E2E8F0;", tokens);
        Assert.Contains("--action-primary-disabled-border: #CBD5E1;", tokens);
        Assert.Contains("--action-primary-disabled-foreground: #475569;", tokens);
        Assert.Contains("--bs-btn-hover-bg: var(--action-primary-hover);", styles);
        Assert.Contains("--bs-btn-active-bg: var(--action-primary-active);", styles);
        Assert.Contains("--bs-btn-disabled-bg: var(--action-primary-disabled-bg);", styles);
        Assert.Contains("background: #d2aa52;", registrationStyles);
        Assert.DoesNotContain(":is(.btn-primary, .btn-gold)", tokens);
        Assert.Contains("html:not([data-theme=\"dark\"]) .btn-primary:active", styles);
        Assert.Contains("html[data-theme=\"dark\"] .btn-primary:active", styles);
        Assert.Contains("html:not([data-theme=\"dark\"]) .btn-primary:is(:disabled, .disabled)", styles);
        Assert.Contains(".admin-authenticated a.admin-primary-action", styles);
        Assert.Contains(".admin-registration-form-page:has(input[name=\"Id\"][value=\"0\"]) .admin-primary-action", styles);
        Assert.Contains(".admin-form-page:has(input[name=\"Id\"][value=\"0\"]) .admin-form-actions > .btn-gold", styles);
        Assert.Contains(".admin-opportunity-create .registration-actions > .btn-gold", styles);
        Assert.Contains("outline: 3px solid rgba(37, 99, 235, .25);", styles);
        Assert.Contains("color: inherit;", styles);
    }

    [Fact]
    public void Main_administrative_create_actions_use_shared_primary_classes_or_styles()
    {
        AssertViewContains("Users", "Index.cshtml", "admin-users-primary\" asp-action=\"Create\"");
        AssertViewContains("Users", "Create.cshtml", "admin-users-primary\" type=\"submit\">Criar usuário");
        AssertViewContains("Categories", "Index.cshtml", "admin-primary-action");
        AssertViewContains("Products", "Index.cshtml", "admin-primary-action");
        AssertViewContains("PaymentTerms", "Index.cshtml", "admin-primary-action");
        AssertViewContains("PriceTables", "Index.cshtml", "admin-primary-action");
        AssertViewContains("SalesRepresentatives", "Index.cshtml", "admin-primary-action");
        AssertViewContains("Categories", "Edit.cshtml", "class=\"btn admin-primary-action\"");
        AssertViewContains("PaymentTerms", "Edit.cshtml", "class=\"btn admin-primary-action\"");
        AssertViewContains("PriceTables", "Edit.cshtml", "class=\"btn admin-primary-action\"");
        AssertViewContains("Drivers", "Index.cshtml", "btn-primary");
        AssertViewContains("PaymentTerminals", "Index.cshtml", "btn-primary");
        AssertViewContains("DriverPaymentTerminalAssignments", "Index.cshtml", "btn-primary");

        var styles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "primary-action-buttons.css"));
        Assert.Contains(".commercial-dashboard-hero > .btn-gold", styles);
        Assert.Contains(".commercial-calendar-hero > .btn-gold", styles);
        Assert.Contains(".customer-commercial-page a.btn-gold", styles);
        Assert.Contains(".portal-top:has(+ .customer-opportunity-list) > .btn-gold", styles);
        Assert.Contains("#assisted-submit", styles);
        Assert.Contains(".admin-opportunity-create .registration-actions > .btn-gold", styles);
        Assert.Contains("admin-opportunity-create", ReadView("Opportunities", "Create.cshtml"));
        Assert.Contains("class=\"btn btn-gold\"", ReadView("Customers", "Edit.cshtml"));
        Assert.DoesNotContain(".admin-image-manager .admin-primary-action", styles);
        Assert.Contains("class=\"btn btn-primary\">Enviar para an&aacute;lise", File.ReadAllText(Path.Combine(WebProjectPath, "Views", "CustomerRegistration", "Register.cshtml")));
    }

    [Fact]
    public void Filters_cancellation_and_destructive_actions_keep_non_primary_semantics()
    {
        var styles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "primary-action-buttons.css"));
        Assert.Contains(".customer-list-tools .admin-primary-action", styles);
        Assert.Contains(".orders-filter-grid .admin-primary-action", styles);
        var registrationStyles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "admin-registrations.css"));
        Assert.Contains(".admin-secondary-action", registrationStyles);
        Assert.Contains("class=\"btn admin-secondary-action erp-filter-submit\"", ReadView("Integrations", "Index.cshtml"));
        Assert.Contains(".commercial-calendar-filters .btn-primary", styles);
        Assert.Contains("html:not([data-theme=\"dark\"]) .commercial-calendar-filters .btn-primary", styles);
        Assert.Contains(".order-details-status-form .btn-primary", styles);
        Assert.Contains("admin-users-secondary\">Cancelar", ReadView("Users", "Create.cshtml"));
        Assert.Contains("class=\"btn btn-danger\"", ReadView("Users", "Index.cshtml"));
        Assert.Contains("btn-outline-danger", ReadView("Drivers", "Index.cshtml"));
        Assert.Contains("class=\"btn admin-primary-action\"", ReadView("Integrations", "Wmc.cshtml"));
    }

    [Fact]
    public void Targeted_edit_forms_use_shared_actions_and_terminal_form_structure()
    {
        Assert.Contains("class=\"btn admin-secondary-action\"", ReadView("Products", "Edit.cshtml"));
        Assert.Contains("class=\"btn admin-primary-action\"", ReadView("Products", "Edit.cshtml"));
        Assert.Contains("class=\"btn admin-secondary-action\"", ReadView("SalesRepresentatives", "Edit.cshtml"));
        Assert.Contains("class=\"btn admin-primary-action\"", ReadView("SalesRepresentatives", "Edit.cshtml"));
        Assert.Contains("class=\"btn admin-secondary-action\"", ReadView("Categories", "Edit.cshtml"));
        Assert.Contains("class=\"btn admin-secondary-action\"", ReadView("PriceTables", "Edit.cshtml"));
        Assert.Contains("class=\"btn admin-secondary-action\"", ReadView("PaymentTerms", "Edit.cshtml"));

        var terminal = ReadView("PaymentTerminals", "Edit.cshtml");
        Assert.Contains("admin-form-page admin-registration-form-page", terminal);
        Assert.Contains("admin-form-card", terminal);
        Assert.Contains("admin-form-actions", terminal);
        Assert.Contains("admin-check", terminal);
        Assert.Contains("admin-primary-action", terminal);
        Assert.Contains("admin-secondary-action", terminal);
        Assert.DoesNotContain("btn-primary", terminal);
        Assert.DoesNotContain("btn-outline-secondary", terminal);
    }

    [Fact]
    public void Opportunities_and_notifications_render_empty_states_and_friendly_labels()
    {
        var opportunities = ReadView("Opportunities", "Index.cshtml");
        Assert.Contains("Nenhuma oportunidade encontrada", opportunities);
        Assert.Contains("Ainda não existem oportunidades para os filtros selecionados.", opportunities);
        Assert.Contains("Aberta", opportunities);
        Assert.Contains("Negotiation => \"Em negociação\"", opportunities);
        Assert.Contains("@opportunity.Type.ToDisplayName()", opportunities);
        Assert.Contains("Contacted => \"Em contato\"", opportunities);
        Assert.Contains("Won => \"Ganha\"", opportunities);
        Assert.Contains("Lost => \"Perdida\"", opportunities);
        Assert.Contains("Cancelled => \"Cancelada\"", opportunities);
        Assert.DoesNotContain(">@opportunity.Stage<", opportunities);

        var notifications = ReadView("Notifications", "Index.cshtml");
        Assert.Contains("Nenhuma notifica&ccedil;&atilde;o", notifications);
        Assert.Contains("N&atilde;o h&aacute; notifica&ccedil;&otilde;es para exibir no momento.", notifications);
    }

    [Fact]
    public void Registration_form_icons_use_available_semantic_font_awesome_classes()
    {
        var priceTables = ReadView("PriceTables", "Edit.cshtml");
        var paymentTerms = ReadView("PaymentTerms", "Edit.cshtml");
        var fontAwesome = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "lib", "fontawesome", "css", "all.min.css"));

        Assert.Contains("fa-table-list", priceTables);
        Assert.Contains(".fa-table-list", fontAwesome);
        Assert.Contains("fa-calendar-days", paymentTerms);
        Assert.Contains(".fa-calendar-days", fontAwesome);
        Assert.Contains("fa-user-tie", ReadView("SalesRepresentatives", "Edit.cshtml"));
    }

    [Fact]
    public void WhatsApp_send_button_keeps_its_existing_class_and_styles()
    {
        var view = File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Views", "WhatsApp", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "whatsapp-composer.css"));

        Assert.Contains("class=\"btn whatsapp-send-button\"", view);
        Assert.Contains(".whatsapp-send-button", styles);
        Assert.DoesNotContain("whatsapp-send-button", File.ReadAllText(Path.Combine(WebProjectPath, "wwwroot", "css", "primary-action-buttons.css")));
    }

    [Fact]
    public void White_primary_foreground_meets_wcag_aa_for_normal_hover_and_active_states()
    {
        Assert.True(ContrastRatio("#FFFFFF", "#2563EB") >= 4.5);
        Assert.True(ContrastRatio("#FFFFFF", "#1D4ED8") >= 4.5);
        Assert.True(ContrastRatio("#FFFFFF", "#1E40AF") >= 4.5);
        Assert.True(ContrastRatio("#475569", "#E2E8F0") >= 4.5);
    }

    private static void AssertViewContains(string folder, string fileName, string expected) =>
        Assert.Contains(expected, ReadView(folder, fileName));

    private static string ReadView(string folder, string fileName) =>
        File.ReadAllText(Path.Combine(WebProjectPath, "Areas", "Admin", "Views", folder, fileName));

    private static double ContrastRatio(string foreground, string background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + .05) / (darker + .05);
    }

    private static double RelativeLuminance(string color)
    {
        var red = Convert.ToInt32(color.Substring(1, 2), 16) / 255d;
        var green = Convert.ToInt32(color.Substring(3, 2), 16) / 255d;
        var blue = Convert.ToInt32(color.Substring(5, 2), 16) / 255d;
        return .2126 * Linearize(red) + .7152 * Linearize(green) + .0722 * Linearize(blue);
    }

    private static double Linearize(double value) =>
        value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
}
