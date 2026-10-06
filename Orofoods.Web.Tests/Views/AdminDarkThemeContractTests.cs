using System.Text.Json;
using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Views;

public class AdminDarkThemeContractTests
{
    private const string DarkThemeGate = "html[data-theme=\"dark\"]";
    private const string AdminBodyGate = "body.admin-authenticated:not(:has(.whatsapp-inbox-page))";

    [Fact]
    public void AdminFooterDarkRulesRequireAdminThemeAndBody()
    {
        var styles = ReadStyles("admin-shell.css");

        RequireScopedRule(styles, ".site-footer", "background");
        RequireScopedRule(styles, ".site-footer", "color");
        RequireScopedRule(styles, ".site-footer", ".footer-brand p", "color");
        RequireScopedRule(styles, ".site-footer", ".footer-links a", "color");
        RequireScopedRule(styles, ".site-footer", ".footer-legal small", "color");
        RequireScopedValue(styles, ".site-footer", "background", "#0F172A");
        RequireScopedValue(styles, ".site-footer", "border-top", "1px solid #334155");
        RequireScopedValue(styles, ".site-footer", "padding", "16px 0");
        RequireScopedValue(styles, ".site-footer .footer-logo", "width", "44px");
        RequireScopedValue(styles, ".site-footer .footer-logo", "height", "44px");
        RequireScopedValue(styles, ".site-footer .site-footer-grid", "gap", "18px");
        RequireScopedValue(styles, ".site-footer .site-footer-grid", "grid-template-columns", "1fr");
        Assert.Matches(@"(?s)@media\s*\(max-width:\s*700px\).*?" + Regex.Escape(DarkThemeGate + " " + AdminBodyGate + " .site-footer .site-footer-grid") + @"\s*\{[^}]*grid-template-columns:\s*1fr", styles);
        RequireScopedValue(styles, ".site-footer .footer-brand p", "color", "#CBD5E1");
        RequireScopedValue(styles, ".site-footer .footer-links a", "color", "#38BDF8");
        RequireScopedValue(styles, ".site-footer .footer-links a:hover", "color", "#F8FAFC");
        RequireScopedRule(styles, ".site-footer", ".footer-links a:focus-visible", "outline");
        RequireScopedValue(styles, ".site-footer .footer-legal i", "color", "#F59E0B");
        AssertAdminDarkRulesExcludeWhatsApp(styles, "admin-shell.css");
    }

    [Fact]
    public void PublicFooterRulesRemainInSiteStylesheet()
    {
        var rules = ReadRules(ReadStyles("site.css"));

        Assert.Contains(rules, rule => rule.Selector == ".site-footer" &&
            HasDeclaration(rule, "background", "#191d18") && HasDeclaration(rule, "color", "#cfd4c9"));
        Assert.Contains(rules, rule => rule.Selector == ".site-footer .site-footer-grid" &&
            HasDeclaration(rule, "display", "grid!important") && HasDeclaration(rule, "gap", "28px"));
        Assert.Contains(rules, rule => rule.Selector == ".footer-brand p" && HasDeclaration(rule, "color", "#c4c9bf"));
        Assert.Contains(rules, rule => rule.Selector == ".site-footer a" && HasDeclaration(rule, "color", "#d8b25f"));
        Assert.Contains(rules, rule => rule.Selector == ".footer-legal small" && HasDeclaration(rule, "color", "#8f978a"));
        Assert.DoesNotContain(rules, rule => rule.Selector.Contains(DarkThemeGate, StringComparison.Ordinal) &&
            rule.Selector.Contains(".site-footer", StringComparison.Ordinal));
    }

    [Fact]
    public void AdminNavigationAndAccountRulesAreScoped()
    {
        var navigationStyles = ReadStyles("admin-navigation.css");
        var headerStyles = ReadStyles("header.css");

        RequireScopedRule(navigationStyles, ".admin-sidebar", "background");
        RequireScopedRule(navigationStyles, ".admin-nav-link", "color");
        RequireScopedRule(navigationStyles, ".admin-nav-link:hover", "color");
        RequireScopedRule(navigationStyles, ".admin-nav-link:focus-visible", "outline");
        RequireScopedRule(navigationStyles, ".admin-offcanvas", "background");
        RequireScopedRule(headerStyles, ".site-header-modern", ".account-menu", "background");
        RequireScopedRule(headerStyles, ".site-header-modern", ".account-menu .dropdown-item", "color");
        RequireScopedRule(headerStyles, ".site-header-modern", ".account-menu .dropdown-item:hover", "background");
        RequireScopedRule(headerStyles, ".site-header-modern", ".account-menu .dropdown-item:focus", "background");
        RequireScopedValue(navigationStyles, ".admin-sidebar", "background", "#111827");
        RequireScopedValue(navigationStyles, ".admin-nav-link", "color", "#CBD5E1");
        RequireScopedValue(navigationStyles, ".admin-nav-link > i", "color", "#94A3B8");
        RequireScopedValue(navigationStyles, ".admin-nav-link:hover", "background", "#1E293B");
        RequireScopedValue(navigationStyles, ".admin-nav-link.active", "color", "#F8FAFC");
        RequireScopedValue(navigationStyles, ".admin-nav-link.active > i", "color", "var(--admin-sidebar-accent)");
        RequireScopedRule(navigationStyles, ".admin-nav-group:focus-visible", "outline");
        RequireScopedValue(navigationStyles, ".admin-nav-section-title", "color", "#94A3B8");
        RequireScopedValue(navigationStyles, ".admin-nav-chevron", "color", "#94A3B8");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu", "background", "#111827");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu", "border-color", "#334155");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu .dropdown-item", "color", "#E5E7EB");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu .dropdown-item:hover", "background", "#1E293B");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu .dropdown-item:focus", "background", "#1E293B");
        RequireScopedRule(headerStyles, ".site-header-modern", ".account-menu .dropdown-item:focus-visible", "outline");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu i", "color", "#F59E0B");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu .account-logout", "color", "#FCA5A5");
        RequireScopedValue(headerStyles, ".site-header-modern .account-menu .dropdown-divider", "border-color", "#334155");
        AssertAdminDarkRulesExcludeWhatsApp(navigationStyles, "admin-navigation.css");
        AssertAdminDarkRulesExcludeWhatsApp(headerStyles, "header.css");
    }

    [Fact]
    public void AdminOrdersRulesAreScoped()
    {
        var styles = ReadStyles("admin-orders.css");

        RequireScopedRule(styles, ".admin-orders-page", ".orders-filter-card", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table-card", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".form-control", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".form-select", "color");
        RequireScopedRule(styles, ".admin-orders-page", ".order-status-badge", "color");
        RequireScopedRule(styles, ".admin-orders-page", ".btn-order-open", "color");
        RequireScopedValue(styles, ".admin-orders-page .admin-page-header .eyebrow", "color", "#CBD5E1");
        foreach (var component in new[] { ".orders-filter-grid label", ".orders-search > i", ".orders-list-meta", ".orders-number", ".orders-table tbody td", ".orders-table tbody td::before" })
            RequireScopedRule(styles, ".admin-orders-page", component, "color");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table thead th", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table tbody tr", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table tbody tr:hover", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table", "--bs-table-bg");
        RequireScopedRule(styles, ".admin-orders-page", ".btn-order-open:hover", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".btn-order-open:focus-visible", "outline");
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table thead a:focus-visible", "outline");
        RequireScopedRule(styles, ".admin-orders-page", ".page-link", "background");
        RequireScopedRule(styles, ".admin-orders-page", ".page-item.active .page-link", "color");
        RequireScopedRule(styles, ".admin-orders-page", ".page-item.disabled .page-link", "color");
        RequireScopedRule(styles, ".admin-orders-page", ".page-link:focus-visible", "outline");
        AssertAdminDarkRulesExcludeWhatsApp(styles, "admin-orders.css");
    }

    [Theory]
    [InlineData("draft", "#CBD5E1")]
    [InlineData("received", "#FCD34D")]
    [InlineData("underreview", "#FCD34D")]
    [InlineData("approved", "#6EE7B7")]
    [InlineData("picking", "#6EE7B7")]
    [InlineData("invoiced", "#6EE7B7")]
    [InlineData("outfordelivery", "#6EE7B7")]
    [InlineData("delivered", "#6EE7B7")]
    [InlineData("cancelled", "#FCA5A5")]
    public void AdminOrdersStatusBadgesKeepSemanticColors(string status, string textColor)
    {
        var styles = ReadStyles("admin-orders.css");
        var component = $".admin-orders-page .order-status-badge--{status}";
        RequireScopedValue(styles, component, "color", textColor);
        RequireScopedRule(styles, component, "background");
        RequireScopedRule(styles, component, "border-color");
    }

    [Fact]
    public void AdminOrderDetailsScopeCoversPaymentWmcHistoryAndItems()
    {
        var view = ReadOrderView("Details");
        Assert.Matches("<section class=\"section admin-order-details-page\">", view);
        var styles = ReadStyles("admin-orders.css");
        foreach (var component in new[] { ".card", ".card-body", ".commercial-bar", ".list-group-item", ".order-items", ".order-row", ".alert-info", ".alert-warning", ".alert-success" })
        {
            RequireScopedRule(styles, ".admin-order-details-page", component, "background");
            RequireScopedRule(styles, ".admin-order-details-page", component, "color");
        }
        foreach (var component in new[] { ".portal-top small", ".commercial-bar small", ".list-group-item small", ".list-group-item time", ".order-row small", "[data-card-on-delivery-details] dt", "[data-card-on-delivery-details] dd", "[data-point-payment-status]", "code" })
            RequireScopedRule(styles, ".admin-order-details-page", component, "color");
        RequireScopedRule(styles, ".admin-order-details-page", ".text-muted", "color");
        RequireScopedValue(styles, ".admin-order-details-page .text-danger", "color", "#FCA5A5");
        RequireScopedValue(styles, ".admin-order-details-page .btn-outline-danger", "color", "#FCA5A5");
        RequireScopedRule(styles, ".admin-order-details-page", ".btn-outline-danger:hover", "background");
        RequireScopedValue(styles, ".admin-order-details-page .btn-outline-danger:focus-visible", "color", "#F8FAFC");
        RequireScopedRule(styles, ".admin-order-details-page", ".btn-outline-dark:hover", "background");
        RequireScopedRule(styles, ".admin-order-details-page", ".btn-outline-dark:disabled", "color");
        RequireScopedRule(styles, ".admin-order-details-page", ".btn:focus-visible", "outline");
        AssertAdminDarkRulesExcludeWhatsApp(styles, "admin-orders.css");
    }

    [Theory]
    [InlineData(".admin-orders-page", ".form-control")]
    [InlineData(".admin-orders-page", ".form-select")]
    [InlineData(".admin-order-details-page", ".form-select")]
    public void AdminOrderControlsHaveDarkSurfacesAndVisibleFocus(string owner, string control)
    {
        var styles = ReadStyles("admin-orders.css");
        RequireScopedRule(styles, owner, control, "background");
        RequireScopedRule(styles, owner, control, "color");
        RequireScopedRule(styles, owner, control, "border-color");
        RequireScopedRule(styles, owner, control + ":focus", "outline");
        RequireScopedRule(styles, owner, control + ":disabled", "background");
        RequireScopedRule(styles, owner, control + ":disabled", "color");
    }

    [Fact]
    public void AdminOrdersRetainResponsiveActionsAndExistingPaymentAndWmcHooks()
    {
        var list = ReadOrderView("Index");
        var detail = ReadOrderView("Details");
        Assert.Contains("<div class=\"table-responsive\"><table class=\"table orders-table\">", list);
        Assert.Contains("data-label=\"Ação\"", list);
        Assert.Contains("class=\"btn btn-sm btn-order-open\" asp-action=\"Details\" asp-route-id=\"@order.Id\"", list);
        Assert.Contains("admin-primary-action orders-filter-submit", list);
        Assert.Contains("order.Status.ToString().ToLowerInvariant()", list);
        Assert.Contains("order-status-badge--@statusClass", list);
        Assert.Contains("id=\"orderQuery\"", list);
        Assert.Contains("id=\"orderStatus\"", list);
        foreach (var hook in new[] { "data-payment-status", "data-card-on-delivery-details", "data-point-payment-poll", "data-point-status-label", "data-delivered-payment-guard", "id=\"point-assignment\"", "admin-point-payment.js", "Model.StatusHistory.OrderByDescending", "Model.WmcExportAudits.OrderByDescending", "entry.FileName", "entry.Error", "item.Subtotal" })
            Assert.Contains(hook, detail);
        foreach (var action in new[] { "UpdateStatus", "ReprocessIntegration", "PollPointCharge", "RefreshPointCharge", "CancelPointCharge", "StartPointCharge" })
            Assert.Contains($"asp-action=\"{action}\" method=\"post\"", detail);
        Assert.Contains("<button class=\"btn btn-gold\">Atualizar status</button>", detail);
        Assert.DoesNotContain("class=\"modal", list + detail);
        var styles = ReadStyles("admin-orders.css");
        Assert.Contains("@media(max-width:767px)", styles);
        Assert.Contains("content:attr(data-label)", styles);
        Assert.Contains(".orders-table tbody td[data-label=\"Ação\"] .btn{width:100%}", styles);
        Assert.DoesNotContain(ReadRules(styles).Where(rule => IsScopedAdminDarkSelector(rule.Selector)),
            rule => ContainsComponent(rule.Selector, ".btn-gold") || ContainsComponent(rule.Selector, ".admin-primary-action") || ContainsComponent(rule.Selector, ".modal"));
    }

    [Fact]
    public void WhatsAppThemeRemainsOutsideAdminDarkRules()
    {
        var projectPath = WebProjectPath();
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        // Losing the route guard, either valid stored value, or its application leaks the Admin default into CRM.
        Assert.Matches(
            "(?s)@if \\(currentArea == \\\"Admin\\\" && currentController == \\\"WhatsApp\\\" && currentAction == \\\"Index\\\"\\)\\s*\\{\\s*<script>.*?localStorage\\.getItem\\('orofoods\\.crm\\.theme'\\).*?if \\(savedCrmTheme === 'dark' \\|\\| savedCrmTheme === 'light'\\)\\s*\\{\\s*document\\.documentElement\\.dataset\\.theme = savedCrmTheme;.*?</script>",
            layout);

        foreach (var stylesheet in new[] { "admin-shell.css", "admin-navigation.css", "header.css", "admin-orders.css" })
        {
            var styles = ReadStyles(stylesheet);
            Assert.True(ReadRules(styles).Any(rule => IsScopedAdminDarkSelector(rule.Selector)),
                $"{stylesheet} must provide Admin dark rules that exclude .whatsapp-inbox-page.");
            AssertAdminDarkRulesExcludeWhatsApp(styles, stylesheet);
        }
    }

    [Theory]
    [InlineData("html[data-theme=\"dark\"] .account-menu { background: white; }", "header.css")]
    [InlineData("body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .account-menu { background: white; }", "header.css")]
    [InlineData(".account-menu { background: white; }", "header.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated .account-menu { background: white; }", "header.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) * { color: white; }", "header.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) :not(.account-menu) { color: white; }", "header.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .table { color: white; }", "admin-orders.css")]
    [InlineData(".orders-table { background: white; }", "admin-orders.css")]
    [InlineData(":root { background: white; }", "admin-orders.css")]
    [InlineData(":root { --surface: #111827; }", "admin-orders.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) :not(.admin-order-details-page) .card { background: white; }", "admin-orders.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .card { background: white; }", "admin-orders.css")]
    [InlineData("html[data-theme=\"dark\"] body.admin-authenticated .admin-order-details-page .card { background: white; }", "admin-orders.css")]
    public void ScopeAuditRejectsAdditionalRulesWithoutAllBoundaries(string additionalRule, string stylesheet)
    {
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertAdminDarkRulesExcludeWhatsApp(ReadStyles(stylesheet) + additionalRule, stylesheet));
    }

    [Theory]
    [InlineData("admin-shell.css")]
    [InlineData("admin-navigation.css")]
    [InlineData("header.css")]
    [InlineData("admin-orders.css")]
    public void ScopeAuditAcceptsTheExplicitHistoricalBaseline(string stylesheet)
    {
        AssertAdminDarkRulesExcludeWhatsApp(ReadStyles(stylesheet), stylesheet);
    }

    [Theory]
    [InlineData(".orders-table", "background-position: center;", "background")]
    [InlineData(".orders-table", "outline-offset: 2px;", "outline")]
    [InlineData(".orders-table", "outline-color: white;", "outline")]
    [InlineData(".orders-table", "outline: 0 solid white;", "outline")]
    [InlineData(".orders-table", "outline-width: 0!important; outline-style: solid; outline-color: white;", "outline")]
    [InlineData(":not(.orders-table)", "background: white;", "background")]
    public void RequiredRuleRejectsIrrelevantDeclarationsAndNegatedComponents(string target, string declarations, string property)
    {
        var styles = $"{DarkThemeGate} {AdminBodyGate} .admin-orders-page {target} {{ {declarations} }}";
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            RequireScopedRule(styles, ".admin-orders-page", ".orders-table", property));
    }

    [Theory]
    [InlineData("background-color: #111827;", "background")]
    [InlineData("outline: 2px solid #38BDF8;", "outline")]
    [InlineData("outline-width: 2px; outline-style: solid; outline-color: #38BDF8;", "outline")]
    public void RequiredRuleAcceptsActualSurfaceAndVisibleFocusDeclarations(string declarations, string property)
    {
        var styles = $"{DarkThemeGate} {AdminBodyGate} .admin-orders-page .orders-table {{ {declarations} }}";
        RequireScopedRule(styles, ".admin-orders-page", ".orders-table", property);
        AssertAdminDarkRulesExcludeWhatsApp(styles, "admin-orders.css");
    }

    [Fact]
    public void CommercialIndexCoversMetricsRoutineKanbanAgendaAndEmptyStates()
    {
        var styles = ReadStyles("commercial.css");
        RequireScopedValue(styles, ".commercial-dashboard-page", "background", "#0F172A");
        foreach (var component in new[] { ".commercial-metric-grid article", ".commercial-routine-panel", ".commercial-priority-legend", ".commercial-attention-panel", ".commercial-attention-card", ".commercial-kanban-panel", ".commercial-kanban-column", ".commercial-activity-card", ".commercial-agenda-panel", ".commercial-kanban-column > header strong" })
            RequireScopedRule(styles, ".commercial-dashboard-page", component, "background");
        foreach (var component in new[] { ".commercial-dashboard-hero p", ".commercial-metric-grid small", ".commercial-metric-grid span", ".commercial-panel-heading > span", ".commercial-routine-item small", ".commercial-routine-item strong", ".commercial-routine-item span", ".commercial-priority-legend > div", ".commercial-attention-card small", ".commercial-kanban-column > header", ".commercial-activity-card small", ".commercial-activity-card span", ".commercial-agenda-item span", ".commercial-empty-column" })
            RequireScopedRule(styles, ".commercial-dashboard-page", component, "color");
        RequireScopedValue(styles, ".commercial-dashboard-page .commercial-attention-card--urgent strong", "color", "#FCA5A5");
        RequireScopedRule(styles, ".commercial-dashboard-page", ".commercial-activity-card:hover", "background");
        RequireScopedRule(styles, ".commercial-dashboard-page", ".commercial-attention-card:focus-visible", "outline");
    }

    [Fact]
    public void CommercialCalendarCoversDaysFiltersTodayEventsAndEmptyStates()
    {
        var styles = ReadStyles("commercial.css");
        RequireScopedValue(styles, ".commercial-calendar-page", "background", "#0F172A");
        foreach (var component in new[] { ".commercial-calendar-filters", ".commercial-calendar-toolbar", ".commercial-calendar-grid", ".commercial-calendar-day", ".commercial-calendar-day > header", ".commercial-calendar-day.is-today > header", ".commercial-calendar-event" })
            RequireScopedRule(styles, ".commercial-calendar-page", component, "background");
        foreach (var component in new[] { ".commercial-calendar-hero p", ".commercial-calendar-filters label", ".commercial-calendar-day > header span", ".commercial-calendar-event time", ".commercial-calendar-event span", ".commercial-calendar-empty" })
            RequireScopedRule(styles, ".commercial-calendar-page", component, "color");
        RequireScopedRule(styles, ".commercial-calendar-page", ".commercial-calendar-toolbar > a:not(.btn):hover", "background");
        RequireScopedRule(styles, ".commercial-calendar-page", ".commercial-calendar-event:focus-visible", "outline");
    }

    [Theory]
    [InlineData("event-whatsapp", "#6EE7B7")]
    [InlineData("event-visit", "#C4B5FD")]
    [InlineData("event-return", "#FCD34D")]
    [InlineData("event-order", "#FCA5A5")]
    public void CommercialCalendarEventTypesKeepDistinctDarkColors(string eventType, string color)
    {
        var styles = ReadStyles("commercial.css");
        var component = ".commercial-calendar-page .commercial-calendar-event." + eventType;
        RequireScopedValue(styles, component, "color", color);
        RequireScopedRule(styles, component, "background");
        RequireScopedRule(styles, component, "border-color");
    }

    [Fact]
    public void CommercialCreateCoversRegistrationLabelsCheckboxAndValidation()
    {
        var styles = ReadStyles("commercial.css");
        RequireScopedRule(styles, ".commercial-form-page", ".commercial-form-card", "background");
        foreach (var component in new[] { ".portal-top small", ".form-label", ".form-check-label" })
            RequireScopedRule(styles, ".commercial-form-page", component, "color");
        RequireScopedValue(styles, ".commercial-form-page .text-danger", "color", "#FCA5A5");
        RequireScopedRule(styles, ".commercial-form-page", ".form-check-input", "background");
        RequireScopedRule(styles, ".commercial-form-page", ".form-check-input:checked", "background");
        RequireScopedRule(styles, ".commercial-form-page", ".form-check-input:focus", "outline");
    }

    [Fact]
    public void CustomersIndexCoversTablesMobileLabelsPaginationAndActions()
    {
        var styles = ReadStyles("admin-customers.css");
        foreach (var component in new[] { ".customer-list-tools", ".customer-list-pending", ".customer-list-panel", ".customer-list-panel > header", ".customer-list-table", ".customer-list-table thead th", ".customer-list-table tbody tr", ".customer-list-table tbody tr:hover", ".page-link", ".page-item.active .page-link", ".page-item.disabled .page-link" })
            RequireScopedRule(styles, ".customer-list-page", component, "background");
        foreach (var component in new[] { ".customer-list-hero p", ".customer-list-tools label", ".customer-search > i", ".customer-list-pending small", ".customer-list-pending strong", ".customer-list-panel > header small", ".customer-list-panel > header > span", ".customer-list-table tbody td", ".customer-list-table tbody td::before", ".customer-list-table tbody td:first-child strong", ".customer-list-table tbody td:first-child small", ".page-item.disabled .page-link" })
            RequireScopedRule(styles, ".customer-list-page", component, "color");
        RequireScopedRule(styles, ".customer-list-page", ".customer-list-table", "--bs-table-bg");
        RequireScopedRule(styles, ".customer-list-page", ".customer-list-table thead a:focus-visible", "outline");
        RequireScopedRule(styles, ".customer-list-page", ".page-link:focus-visible", "outline");
        RequireScopedRule(styles, ".customer-list-page", ".customer-list-table .btn-gold:disabled", "background");
        Assert.DoesNotContain("!important", ReadStyles("admin-customer-actions.css"));
        RequireScopedRule(styles, ".customer-list-page", ".customer-list-table tbody td[data-label=\"Ação\"]", "--customer-action-border");
    }

    [Theory]
    [InlineData("pending", "#FCD34D")]
    [InlineData("approved", "#6EE7B7")]
    [InlineData("blocked", "#FCA5A5")]
    [InlineData("inactive", "#FCA5A5")]
    public void CustomersIndexStatusBadgesKeepSemanticColors(string status, string color)
    {
        var styles = ReadStyles("admin-customers.css");
        RequireScopedValue(styles, ".customer-list-page .customer-status--" + status, "color", color);
        RequireScopedRule(styles, ".customer-list-page", ".customer-status--" + status, "background");
    }

    [Fact]
    public void CustomersDetailsCoversEveryTabPipelineHistoryAndEmptyStates()
    {
        var styles = ReadStyles("admin-customers.css");
        foreach (var component in new[] { ".customer-commercial-metrics article", ".customer-commercial-attention", ".customer-opportunities-panel", ".customer-detail-panel", ".customer-finance-metrics article", ".customer-detail-table", ".customer-history-timeline i", ".admin-order-status" })
            RequireScopedRule(styles, ".customer-commercial-page", component, "background");
        foreach (var component in new[] { ".customer-commercial-hero p", ".customer-commercial-metrics small", ".customer-commercial-attention dt", ".customer-commercial-attention dd", ".customer-opportunity-list small", ".customer-detail-panel dt", ".customer-detail-panel dd", ".customer-detail-panel-heading > span", ".customer-detail-table thead th", ".customer-detail-table tbody td", ".customer-detail-table small", ".customer-finance-metrics small", ".customer-finance-metrics strong", ".customer-history-timeline strong", ".customer-history-timeline span", ".customer-history-timeline time", ".customer-commercial-empty" })
            RequireScopedRule(styles, ".customer-commercial-page", component, "color");
        RequireScopedRule(styles, ".customer-commercial-page", ".customer-detail-table", "--bs-table-bg");
        RequireScopedRule(styles, ".customer-commercial-page", ".customer-detail-tabs .nav-link.active", "border-color");
        RequireScopedRule(styles, ".customer-commercial-page", ".customer-detail-tabs .nav-link:focus-visible", "outline");
        RequireScopedRule(styles, ".customer-commercial-page", ".customer-detail-tabs .nav-link", "--customer-tab-link-color");
        RequireScopedRule(styles, ".customer-commercial-page", ".customer-detail-tabs .nav-link.active", "--customer-tab-link-color");
    }

    [Fact]
    public void CustomersEditCoversApprovalHeroCardsSummaryHintsAndCreditControls()
    {
        var styles = ReadStyles("admin-customers.css");
        RequireScopedValue(styles, ".customer-approval-page", "background", "#0F172A");
        foreach (var component in new[] { ".customer-approval-hero", ".customer-status-panel", ".registration-card", ".registration-card-heading > span", ".customer-approval-summary", ".input-group-text", ".form-check-input", ".form-check-input:checked" })
            RequireScopedRule(styles, ".customer-approval-page", component, "background");
        foreach (var component in new[] { ".customer-approval-hero p", ".customer-status-panel small", ".customer-status-panel strong", ".registration-card-heading small", ".form-label", ".form-label em", ".customer-approval-hint", ".customer-field-help", ".customer-approval-summary li", ".customer-approval-summary > p:last-child" })
            RequireScopedRule(styles, ".customer-approval-page", component, "color");
        RequireScopedValue(styles, ".customer-approval-page .customer-status-panel strong.is-pending", "color", "#FCD34D");
        RequireScopedRule(styles, ".customer-approval-page", ".form-check-input:focus", "outline");
    }

    [Fact]
    public void CustomersNewOrderCoversSearchProductsDynamicLinesSummaryAndValidation()
    {
        var styles = ReadStyles("admin-customers.css");
        foreach (var component in new[] { ".admin-form-card", ".checkout-summary", ".assisted-product-list article" })
            RequireScopedRule(styles, ".assisted-order-page", component, "background");
        foreach (var component in new[] { ".portal-top p", ".portal-top small", ".assisted-product-list small", ".assisted-order-fields label", ".summary-label", ".summary-total", "#assisted-summary-items", ".text-muted" })
            RequireScopedRule(styles, ".assisted-order-page", component, "color");
        RequireScopedValue(styles, ".assisted-order-page .text-danger", "color", "#FCA5A5");
        RequireScopedValue(styles, ".assisted-order-page .assisted-order-line button", "color", "#FCA5A5");
        RequireScopedRule(styles, ".assisted-order-page", ".assisted-order-line button:focus-visible", "outline");
        RequireScopedRule(styles, ".assisted-order-page", "#assisted-submit:disabled", "outline");
    }

    [Theory]
    [InlineData("commercial.css", ".commercial-calendar-page", ".commercial-calendar-filters select")]
    [InlineData("commercial.css", ".commercial-form-page", ".form-control")]
    [InlineData("commercial.css", ".commercial-form-page", ".form-select")]
    [InlineData("admin-customers.css", ".customer-list-page", ".form-control")]
    [InlineData("admin-customers.css", ".customer-list-page", ".form-select")]
    [InlineData("admin-customers.css", ".customer-commercial-page", ".customer-opportunity-list select")]
    [InlineData("admin-customers.css", ".customer-approval-page", ".form-control")]
    [InlineData("admin-customers.css", ".customer-approval-page", ".form-select")]
    [InlineData("admin-customers.css", ".assisted-order-page", ".catalog-tools input")]
    [InlineData("admin-customers.css", ".assisted-order-page", ".assisted-product-list input")]
    [InlineData("admin-customers.css", ".assisted-order-page", ".assisted-order-line input")]
    [InlineData("admin-customers.css", ".assisted-order-page", ".assisted-order-fields select")]
    [InlineData("admin-customers.css", ".assisted-order-page", ".assisted-order-fields input")]
    [InlineData("admin-customers.css", ".assisted-order-page", ".assisted-order-fields textarea")]
    public void CommercialAndCustomersControlsCoverFocusDisabledAndReadonly(string stylesheet, string owner, string control)
    {
        var styles = ReadStyles(stylesheet);
        foreach (var property in new[] { "background", "color", "border-color", "color-scheme" })
            RequireScopedRule(styles, owner, control, property);
        RequireScopedRule(styles, owner, control + ":focus", "outline");
        RequireScopedRule(styles, owner, control + ":disabled", "background");
        RequireScopedRule(styles, owner, control + ":disabled", "color");
        RequireScopedRule(styles, owner, control + "[readonly]", "background");
    }

    [Theory]
    [InlineData("commercial.css", ".commercial-dashboard-page")]
    [InlineData("commercial.css", ".commercial-calendar-page")]
    [InlineData("commercial.css", ".commercial-form-page")]
    [InlineData("admin-customers.css", ".customer-list-page")]
    [InlineData("admin-customers.css", ".customer-commercial-page")]
    [InlineData("admin-customers.css", ".customer-approval-page")]
    [InlineData("admin-customers.css", ".assisted-order-page")]
    public void CommercialAndCustomersActionsHaveKeyboardAndNeutralButtonStates(string stylesheet, string owner)
    {
        var styles = ReadStyles(stylesheet);
        RequireScopedRule(styles, owner, ".btn:focus-visible", "outline");
        RequireScopedRule(styles, owner, ".btn-outline-dark", "color");
        RequireScopedRule(styles, owner, ".btn-outline-dark:hover", "background");
        RequireScopedRule(styles, owner, ".btn-outline-dark:disabled", "background");
    }

    [Theory]
    [InlineData("commercial.css")]
    [InlineData("admin-customers.css")]
    public void CommercialAndCustomersScopeAuditPreservesHistoricalPublicAndModuleRules(string stylesheet)
    {
        var styles = ReadStyles(stylesheet);
        AssertAdminDarkRulesExcludeWhatsApp(styles, stylesheet);
        var baselinePath = Path.Combine(WebProjectPath(), "..", "Orofoods.Web.Tests", "Views", "AdminDarkThemeHistoricalRules.json");
        var baseline = JsonSerializer.Deserialize<Dictionary<string, List<CssRule>>>(File.ReadAllText(baselinePath))!;
        var current = ReadRules(styles).Select(RuleIdentity).ToHashSet();
        Assert.All(baseline[stylesheet], rule => Assert.Contains(RuleIdentity(rule), current));
    }

    [Theory]
    [InlineData("commercial.css", "html[data-theme=\"dark\"] .commercial-calendar-page { background: white; }")]
    [InlineData("commercial.css", "html[data-theme=\"dark\"] body.admin-authenticated .commercial-calendar-page { background: white; }")]
    [InlineData("commercial.css", "html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .whatsapp-inbox { background: white; }")]
    [InlineData("commercial.css", "html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .commercial-attention-panel { background: white; }")]
    [InlineData("admin-customers.css", "html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .form-control { background: white; }")]
    [InlineData("admin-customers.css", "html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) :not(.customer-list-page) .table { background: white; }")]
    public void CommercialAndCustomersScopeAuditRejectsLeaks(string stylesheet, string additionalRule)
    {
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertAdminDarkRulesExcludeWhatsApp(ReadStyles(stylesheet) + additionalRule, stylesheet));
    }

    [Fact]
    public void CommercialAndCustomersKeepViewHooksSemanticActionsAndResponsiveGeometry()
    {
        var views = new[] { ("Commercial", "Calendar", "commercial-calendar-page"), ("Commercial", "Create", "commercial-form-page"), ("Commercial", "Index", "commercial-dashboard-page"), ("Customers", "Details", "customer-commercial-page"), ("Customers", "Edit", "customer-approval-page"), ("Customers", "Index", "customer-list-page"), ("Customers", "NewOrder", "assisted-order-page") };
        foreach (var (folder, view, owner) in views)
        {
            var markup = File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", folder, view + ".cshtml"));
            Assert.Contains(owner, markup);
            Assert.DoesNotContain("@section Head", markup);
            Assert.DoesNotContain("style=", markup);
        }
        var details = File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", "Customers", "Details.cshtml"));
        foreach (var tab in new[] { "overview", "orders", "products", "finance", "prices", "addresses", "users", "seller", "history" })
            Assert.Contains("data-bs-target=\"#" + tab + "\"", details);
        var newOrder = File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", "Customers", "NewOrder.cshtml"));
        foreach (var hook in new[] { "AttemptKey", "data-product-id", "assisted-order-lines", "assisted-summary-items", "assisted-total", "assisted-submit", "assisted-order.js" })
            Assert.Contains(hook, newOrder);
        foreach (var stylesheet in new[] { "commercial.css", "admin-customers.css" })
            Assert.DoesNotContain(ReadRules(ReadStyles(stylesheet)).Where(rule => IsScopedAdminDarkSelector(rule.Selector)), rule =>
                Regex.IsMatch(rule.Declarations, @"(?:^|;)\s*(?:display|grid-template-columns|width|min-width|height|min-height|padding|margin|position)\s*:"));
        PublicFooterRulesRemainInSiteStylesheet();
    }

    [Fact]
    public void CustomersCascadeFallbacksPreservePublicSiteColors()
    {
        var site = ReadStyles("site.css");
        Assert.Contains("color:var(--customer-tab-link-color,#4d4e49)!important", site);
        Assert.Contains("border-top:1px solid var(--customer-action-border,#ece7dd)!important", site);
        Assert.DoesNotContain(ReadRules(site), rule => DeclarationValues(rule, "--customer-tab-link-color").Any() || DeclarationValues(rule, "--customer-action-border").Any());
        var actions = ReadStyles("admin-customer-actions.css");
        Assert.Contains("background-color:#182019;color:#fff", actions);
        Assert.DoesNotContain("color:#fff!important", actions);
    }

    [Fact]
    public void CustomersActionsCascadeRefactorPreservesExistingLightDeclarationValues()
    {
        var baselinePath = Path.Combine(WebProjectPath(), "..", "Orofoods.Web.Tests", "Views", "AdminDarkThemeHistoricalRules.json");
        var baseline = JsonSerializer.Deserialize<Dictionary<string, List<CssRule>>>(File.ReadAllText(baselinePath))!;
        var current = ReadRules(ReadStyles("admin-customer-actions.css")).Select(RuleIdentity).ToArray();
        Assert.Equal(baseline["admin-customer-actions.css"].Select(rule => RuleIdentity(rule).Replace("!important", "")), current);
    }

    [Fact]
    public void ProductsAndInventoryCoverDarkSurfacesControlsAndActions()
    {
        var products = ReadStyles("admin-products.css");
        RequireScopedRule(products, ".admin-products-page", "background");
        RequireScopedRule(products, ".admin-products-page .admin-page-header h1", "color");
        RequireScopedRule(products, ".admin-products-page .admin-page-header p", "color");
        RequireScopedRule(products, ".admin-products-page .admin-table-card", "background");
        RequireScopedRule(products, ".admin-products-page .admin-products-table", "--bs-table-bg");
        RequireScopedRule(products, ".admin-products-page .admin-products-table > thead > tr > th", "background");
        RequireScopedRule(products, ".admin-products-page .admin-products-table tbody td", "color");
        RequireScopedRule(products, ".admin-products-page .admin-products-table > tbody > tr:hover", "background");
        RequireScopedRule(products, ".admin-products-page .status-badge--active", "background");
        RequireScopedRule(products, ".admin-products-page .status-badge--active", "color");
        RequireScopedRule(products, ".admin-products-page .status-badge--inactive", "background");
        RequireScopedRule(products, ".admin-products-page .status-badge--inactive", "color");
        RequireScopedRule(products, ".admin-products-page .catalog-tools input", "background");
        RequireScopedRule(products, ".admin-products-page .catalog-tools input[name=\"q\"]:focus", "outline");
        RequireScopedRule(products, ".admin-products-page .admin-secondary-action:hover", "background");
        RequireScopedRule(products, ".admin-products-page .admin-secondary-action:focus-visible", "outline");
        RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page", "background");
        RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page .admin-form-hero", "background");
        RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page .admin-form-card", "background");
        RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page .admin-image-manager", "background");
        foreach (var control in new[] { ".form-control", ".form-select" })
        {
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control, "background");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control, "color");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control, "border-color");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control, "color-scheme");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control + ":focus", "outline");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control + ":disabled", "background");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control + ":disabled", "color");
            RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page " + control + "[readonly]", "background");
        }
        RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page .btn-outline-dark", "color");
        RequireScopedRule(products, ".admin-products-edit-page", ".admin-form-page .text-danger", "color");

        var inventory = ReadStyles("admin-registrations.css");
        RequireScopedRule(inventory, ".admin-inventory-page", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-page-header h1", "color");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-table-card", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-table", "--bs-table-bg");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-table > thead > tr > th", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-table tbody td", "color");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-table > tbody > tr:hover", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-inventory-adjustment .form-control", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-inventory-adjustment .form-control", "color");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-inventory-adjustment .form-control:focus", "outline");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-inventory-adjustment .form-control:disabled", "color");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-inventory-adjustment .form-control[readonly]", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-count-badge", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-edit-action:hover", "background");
        RequireScopedRule(inventory, ".admin-inventory-page .admin-edit-action:focus-visible", "outline");
        RequireScopedRule(inventory, ".admin-inventory-page .alert-danger", "color");

        AssertModuleDarkRulesAreScoped(products, new[] { ".admin-products-page", ".admin-products-edit-page" });
        AssertModuleDarkRulesAreScoped(inventory, new[] { ".admin-registration-page", ".admin-registration-form-page", ".admin-price-items-page", ".admin-inventory-page" });
        foreach (var stylesheet in new[] { "admin-products.css", "admin-registrations.css" })
            Assert.DoesNotContain(ReadRules(ReadStyles(stylesheet)).Where(rule => IsScopedAdminDarkSelector(rule.Selector)), rule =>
                Regex.IsMatch(rule.Declarations, @"(?:^|;)\s*(?:display|grid-template-columns|width|min-width|height|min-height|padding|margin|position)\s*:"));
    }

    [Fact]
    public void ProductsAndInventoryKeepExistingViewHooksAndStylesheetOwners()
    {
        var projectPath = WebProjectPath();
        var productsIndex = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Products", "Index.cshtml"));
        var productsEdit = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Products", "Edit.cshtml"));
        var inventoryIndex = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Inventory", "Index.cshtml"));
        var adminLayout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminLayout.cshtml"));

        Assert.Contains("admin-products-page", productsIndex);
        Assert.Contains("admin-products-table", productsIndex);
        Assert.Contains("status-badge--active", productsIndex);
        Assert.Contains("admin-form-page", productsEdit);
        Assert.Contains("admin-products-edit-page", productsEdit);
        Assert.Contains("admin-image-manager", productsEdit);
        Assert.Contains("admin-inventory-page", inventoryIndex);
        Assert.Contains("admin-inventory-adjustment", inventoryIndex);
        Assert.Contains("admin-products.css", adminLayout);
        Assert.Contains("admin-registrations.css", adminLayout);
    }

    [Fact]
    public void AuxiliaryRegistrationModulesCoverDarkSurfacesAndControls()
    {
        var registrations = ReadStyles("admin-registrations.css");
        RequireScopedRule(registrations, ".admin-registration-page", "background");
        RequireScopedRule(registrations, ".admin-registration-page .admin-page-header h1", "color");
        RequireScopedRule(registrations, ".admin-registration-page .admin-page-header > div > p:last-child", "color");
        RequireScopedRule(registrations, ".admin-registration-page .admin-table-card", "background");
        RequireScopedRule(registrations, ".admin-registration-page .admin-table", "--bs-table-bg");
        RequireScopedRule(registrations, ".admin-registration-page .admin-table > thead > tr > th", "background");
        RequireScopedRule(registrations, ".admin-registration-page .admin-table tbody td", "color");
        RequireScopedRule(registrations, ".admin-registration-page .admin-table > tbody > tr:hover", "background");
        RequireScopedRule(registrations, ".admin-registration-page .admin-table tbody td::before", "color");
        RequireScopedRule(registrations, ".admin-registration-page .admin-count-badge", "background");
        RequireScopedRule(registrations, ".admin-registration-page .status-badge--active", "color");
        RequireScopedRule(registrations, ".admin-registration-page .status-badge--inactive", "color");
        RequireScopedRule(registrations, ".admin-registration-page .admin-edit-action:hover", "background");
        RequireScopedRule(registrations, ".admin-registration-page .admin-secondary-action:focus-visible", "outline");
        RequireScopedRule(registrations, ".admin-registration-form-page", "background");
        RequireScopedRule(registrations, ".admin-registration-form-page .admin-form-hero", "background");
        RequireScopedRule(registrations, ".admin-registration-form-page .admin-form-card", "background");
        RequireScopedRule(registrations, ".admin-registration-form-page .form-control", "background");
        RequireScopedRule(registrations, ".admin-registration-form-page .form-select", "color");
        RequireScopedRule(registrations, ".admin-registration-form-page .form-control:focus", "outline");
        RequireScopedRule(registrations, ".admin-registration-form-page .form-control:disabled", "color");
        RequireScopedRule(registrations, ".admin-registration-form-page .form-control[readonly]", "background");
        RequireScopedRule(registrations, ".admin-price-items-page .admin-price-items-table .form-control", "background");
        AssertModuleDarkRulesAreScoped(registrations, new[] { ".admin-registration-page", ".admin-registration-form-page", ".admin-price-items-page", ".admin-inventory-page" });

        var users = ReadStyles("admin-users.css");
        RequireScopedRule(users, ".admin-users-page", "background");
        RequireScopedRule(users, ".admin-users-page .admin-users-filters", "background");
        RequireScopedRule(users, ".admin-users-page .form-control", "color");
        RequireScopedRule(users, ".admin-users-page .form-control:focus", "outline");
        RequireScopedRule(users, ".admin-users-page .form-select:disabled", "color");
        RequireScopedRule(users, ".admin-users-page .form-control[readonly]", "background");
        RequireScopedRule(users, ".admin-users-page .admin-users-table", "background");
        RequireScopedRule(users, ".admin-users-page .admin-users-status.is-active", "color");
        RequireScopedRule(users, ".admin-users-page .admin-users-status.is-inactive", "color");
        RequireScopedRule(users, ".admin-users-page .admin-users-action-menu", "background");
        RequireScopedRule(users, ".admin-users-page .admin-users-actions summary:focus-visible", "outline");
        RequireScopedRule(users, ".admin-users-page .modal-content", "background");
        AssertModuleDarkRulesAreScoped(users, new[] { ".admin-users-page" });

        var modules = new Dictionary<string, string[]>
        {
            ["admin-drivers.css"] = [".page-header", ".table-responsive", "form", "h1", "p", ".btn-outline-secondary", ".btn-outline-danger", ".text-danger", ".validation-summary-errors", ".alert-warning"],
            ["admin-driver-payment-terminal-assignments.css"] = [".page-header", ".table-responsive", "form", "h1", ".btn-outline-secondary", ".btn-outline-danger", ".text-danger", ".validation-summary-errors"],
            ["admin-payment-terminals.css"] = [".page-header", ".table-responsive", "form", "h1", ".btn-outline-secondary", ".btn-outline-danger", ".text-danger", ".validation-summary-errors"],
            ["admin-notifications.css"] = [".section", ".portal-top", ".notification-list"],
            ["admin-opportunities.css"] = [".section", ".portal-top", ".customer-opportunity-list", ".admin-opportunity-create", ".btn-outline-dark"]
        };
        foreach (var (stylesheet, owners) in modules)
        {
            var styles = ReadStyles(stylesheet);
            AssertModuleDarkRulesAreScoped(styles, owners);
            Assert.DoesNotContain(ReadRules(styles).Where(rule => IsScopedAdminDarkSelector(rule.Selector)), rule =>
                Regex.IsMatch(rule.Declarations, @"(?:^|;)\s*(?:display|grid-template-columns|width|min-width|height|min-height|padding|margin|position)\s*:"));
        }

        var drivers = ReadStyles("admin-drivers.css");
        RequireScopedRule(drivers, ".page-header", "color");
        RequireScopedRule(drivers, "h1", "color");
        RequireScopedRule(drivers, "p", "color");
        RequireScopedRule(drivers, ".table-responsive table thead th", "background");
        RequireScopedRule(drivers, ".table-responsive table tbody td", "color");
        RequireScopedRule(drivers, "form .form-control", "background");
        RequireScopedRule(drivers, "form .form-control:focus", "outline");
        RequireScopedRule(drivers, ".btn-outline-danger", "color");

        var assignments = ReadStyles("admin-driver-payment-terminal-assignments.css");
        RequireScopedRule(assignments, ".page-header", "color");
        RequireScopedRule(assignments, "h1", "color");
        RequireScopedRule(assignments, ".table-responsive table", "background");
        RequireScopedRule(assignments, ".table-responsive table tbody td", "color");
        RequireScopedRule(assignments, "form .form-select", "background");
        RequireScopedRule(assignments, ".btn-outline-danger", "color");

        var terminals = ReadStyles("admin-payment-terminals.css");
        RequireScopedRule(terminals, ".page-header", "color");
        RequireScopedRule(terminals, "h1", "color");
        RequireScopedRule(terminals, ".table-responsive table", "background");
        RequireScopedRule(terminals, ".table-responsive table tbody td", "color");
        RequireScopedRule(terminals, "form .form-control", "background");
        RequireScopedRule(terminals, ".btn-outline-danger", "color");

        var notifications = ReadStyles("admin-notifications.css");
        RequireScopedRule(notifications, ".portal-top h1", "color");
        RequireScopedRule(notifications, ".notification-list article", "background");
        RequireScopedRule(notifications, ".notification-list article.is-unread", "border-left-color");
        RequireScopedRule(notifications, ".notification-list article p", "color");
        RequireScopedRule(notifications, ".notification-list article button:focus-visible", "outline");

        var opportunities = ReadStyles("admin-opportunities.css");
        RequireScopedRule(opportunities, ".portal-top h1", "color");
        RequireScopedRule(opportunities, ".customer-opportunity-list article", "background");
        RequireScopedRule(opportunities, ".customer-opportunity-list article select", "background");
        RequireScopedRule(opportunities, ".admin-opportunity-create .admin-form-card", "background");
        RequireScopedRule(opportunities, ".admin-opportunity-create .form-control:focus", "outline");
        RequireScopedRule(opportunities, ".admin-opportunity-create .form-select:disabled", "color");
    }

    [Fact]
    public void AuxiliaryModulesLoadOnlyTheirScopedStylesheets()
    {
        var viewsPath = Path.Combine(WebProjectPath(), "Areas", "Admin", "Views");
        var expected = new[]
        {
            ("Drivers", "Index", "admin-drivers.css"),
            ("Drivers", "Edit", "admin-drivers.css"),
            ("DriverPaymentTerminalAssignments", "Index", "admin-driver-payment-terminal-assignments.css"),
            ("DriverPaymentTerminalAssignments", "Create", "admin-driver-payment-terminal-assignments.css"),
            ("PaymentTerminals", "Index", "admin-payment-terminals.css"),
            ("PaymentTerminals", "Edit", "admin-payment-terminals.css"),
            ("Notifications", "Index", "admin-notifications.css"),
            ("Opportunities", "Index", "admin-opportunities.css"),
            ("Opportunities", "Create", "admin-opportunities.css")
        };
        foreach (var (folder, view, stylesheet) in expected)
        {
            var markup = File.ReadAllText(Path.Combine(viewsPath, folder, view + ".cshtml"));
            Assert.Contains("@section Head", markup);
            Assert.Contains(stylesheet, markup);
        }

        foreach (var (folder, view, owner) in new[]
        {
            ("Categories", "Index", "admin-registration-page"),
            ("Categories", "Edit", "admin-registration-form-page"),
            ("PaymentTerms", "Index", "admin-registration-page"),
            ("PaymentTerms", "Edit", "admin-registration-form-page"),
            ("PriceTables", "Index", "admin-registration-page"),
            ("PriceTables", "Edit", "admin-registration-form-page"),
            ("PriceTables", "Items", "admin-price-items-page"),
            ("SalesRepresentatives", "Index", "admin-registration-page"),
            ("SalesRepresentatives", "Edit", "admin-registration-form-page")
        })
            Assert.Contains(owner, File.ReadAllText(Path.Combine(viewsPath, folder, view + ".cshtml")));

        foreach (var view in new[] { "Index", "Create", "Edit", "ResetPassword" })
        {
            var markup = File.ReadAllText(Path.Combine(viewsPath, "Users", view + ".cshtml"));
            Assert.Contains("admin-users-page", markup);
            Assert.Contains("admin-users.css", markup);
        }
    }

    [Fact]
    public void ReportsAndIntegrationsCoverDarkSurfacesControlsAndSemanticStates()
    {
        var reports = ReadStyles("admin-reports.css");
        RequireScopedRule(reports, ".admin-reports-page", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-header h1", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-header > div > p:last-child", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-period-selector", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-period-selector a", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-period-selector .active", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-period-selector button:focus-visible", "outline");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel label", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel .form-control", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel .form-select", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel .form-control:focus", "outline");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel .form-control:disabled", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-filter-panel .form-control[readonly]", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-kpi-grid article", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-kpi-grid strong", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-panel", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-panel header h2", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-chart-unavailable", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-donut::before", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-category-legend strong", "color");
        RequireScopedRule(reports, ".admin-reports-page .report-ranking-position", "background");
        RequireScopedRule(reports, ".admin-reports-page .report-ranking-data strong", "color");
        RequireScopedRule(reports, ".admin-reports-page .report-ranking-track", "background");
        RequireScopedRule(reports, ".admin-reports-page .reports-empty", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-details > summary:focus-visible", "outline");
        RequireScopedRule(reports, ".admin-reports-page .reports-status-list li", "color");
        RequireScopedRule(reports, ".admin-reports-page .reports-status-list li b", "color");
        AssertModuleDarkRulesAreScoped(reports, new[] { ".admin-reports-page" });

        var integrations = ReadStyles("admin-integrations.css");
        RequireScopedRule(integrations, ".admin-integrations-page", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .admin-page-header h1", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-filter-card", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-filter-grid label", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-filter-grid .form-control", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-filter-grid .form-select:focus", "outline");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-filter-grid .form-control:disabled", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-filter-grid .form-control[readonly]", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-table-card", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-table", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-table thead th", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-table tbody td", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-table tbody tr:hover", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-status-badge--pending", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-status-badge--processing", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-status-badge--succeeded", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-status-badge--failed", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-row-actions .btn-erp-export:focus-visible", "outline");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-row-actions .btn-erp-reprocess", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-error-modal", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-error-meta dt", "color");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-error-box", "background");
        RequireScopedRule(integrations, ".admin-integrations-page .erp-empty-state p", "color");
        RequireScopedRule(integrations, ".admin-wmc-page", "background");
        RequireScopedRule(integrations, ".admin-wmc-page .portal-top h1", "color");
        RequireScopedRule(integrations, ".admin-wmc-page .table thead th", "background");
        RequireScopedRule(integrations, ".admin-wmc-page .table tbody td", "color");
        RequireScopedRule(integrations, ".admin-wmc-page .alert-info", "color");
        AssertModuleDarkRulesAreScoped(integrations, new[] { ".admin-integrations-page", ".admin-wmc-page" });

        var wmcView = File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));
        Assert.Contains("admin-wmc-page", wmcView);
        Assert.DoesNotContain("whatsapp-inbox-page", wmcView);
    }

    private static void RequireScopedRule(string styles, string component, string property) =>
        RequireScopedRule(styles, component, component, property);

    private static void RequireScopedValue(string styles, string component, string property, string value) =>
        Assert.True(ReadRules(styles).Any(rule => IsScopedAdminDarkSelector(rule.Selector) &&
            ContainsComponent(rule.Selector, component) && DeclarationValues(rule, property).Contains(value, StringComparer.OrdinalIgnoreCase)),
            $"Missing scoped {property}: {value} for {component}.");

    private static void RequireScopedRule(string styles, string owner, string component, string property)
    {
        Assert.True(ReadRules(styles).Any(rule => IsScopedAdminDarkSelector(rule.Selector) &&
            ContainsComponent(rule.Selector, owner) &&
            ContainsComponent(rule.Selector, component) &&
            HasRequestedDeclaration(rule, property)),
            $"Missing {property} rule for {owner} / {component}, gated by {DarkThemeGate} {AdminBodyGate}.");
    }

    private static void AssertAdminDarkRulesExcludeWhatsApp(string styles, string stylesheet)
    {
        // Fixed pre-implementation source snapshot; never derive exceptions from the current CSS.
        var baselinePath = Path.Combine(WebProjectPath(), "..", "Orofoods.Web.Tests", "Views", "AdminDarkThemeHistoricalRules.json");
        var baseline = JsonSerializer.Deserialize<Dictionary<string, List<CssRule>>>(File.ReadAllText(baselinePath))!;
        var historicalRules = baseline[stylesheet].Select(RuleIdentity).ToHashSet(StringComparer.Ordinal);
        foreach (var rule in ReadRules(styles).Where(rule => !historicalRules.Contains(RuleIdentity(rule))))
        {
            Assert.True(IsScopedAdminDarkSelector(rule.Selector) && HasPermittedComponentScope(rule.Selector, stylesheet),
                $"Additional/changed {stylesheet} rule requires both gates, WhatsApp exclusion, and positive component/page scope: {rule.Selector}");
        }
    }

    private static void AssertModuleDarkRulesAreScoped(string styles, IReadOnlyList<string> owners)
    {
        foreach (var rule in ReadRules(styles).Where(rule => IsScopedAdminDarkSelector(rule.Selector)))
            Assert.True(owners.Any(owner => ContainsComponent(RemoveNonTargetFunctions(rule.Selector), owner)),
                $"Admin dark selector requires a positive module/page scope: {rule.Selector}");
    }

    private static string RuleIdentity(CssRule rule)
    {
        var declarations = rule.Declarations
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        declarations = Regex.Replace(declarations, @"\s+", " ");
        declarations = Regex.Replace(declarations, @"\s*([:;])\s*", "$1");
        return rule.Selector + "{" + declarations.Trim() + "}";
    }

    private static bool HasPermittedComponentScope(string selector, string stylesheet)
    {
        var positiveSelector = RemoveNonTargetFunctions(selector);
        var target = positiveSelector[(positiveSelector.IndexOf("body.admin-authenticated", StringComparison.Ordinal) + "body.admin-authenticated".Length)..];
        return stylesheet switch
        {
            "commercial.css" => new[] { ".commercial-dashboard-page", ".commercial-calendar-page", ".commercial-form-page" }.Any(component => ContainsComponent(target, component)),
            "admin-customers.css" => new[] { ".customer-list-page", ".customer-commercial-page", ".customer-approval-page", ".assisted-order-page" }.Any(component => ContainsComponent(target, component)),
            "admin-orders.css" => ContainsComponent(target, ".admin-orders-page") || ContainsComponent(target, ".admin-order-details-page"),
            "admin-products.css" => ContainsComponent(target, ".admin-products-page") || ContainsComponent(target, ".admin-products-edit-page"),
            "admin-registrations.css" => ContainsComponent(target, ".admin-inventory-page"),
            "header.css" => Regex.IsMatch(target, @"\.(?:site-header-modern|account-[a-z-]+|global-notification-[a-z-]+)(?![a-zA-Z0-9_-])"),
            "admin-navigation.css" => Regex.IsMatch(target, @"\.admin-(?:sidebar|nav|offcanvas|mobile)[a-z-]*(?![a-zA-Z0-9_-])"),
            "admin-shell.css" => Regex.IsMatch(target, @"\.(?:admin-[a-z-]+|site-footer)(?![a-zA-Z0-9_-])"),
            _ => false
        };
    }

    private static bool HasRequestedDeclaration(CssRule rule, string property) => property switch
    {
        "background" => DeclarationValues(rule, "background").Concat(DeclarationValues(rule, "background-color")).Any(),
        "outline" => HasVisibleOutline(rule),
        _ => DeclarationValues(rule, property).Any()
    };

    private static IEnumerable<string> DeclarationValues(CssRule rule, string property) =>
        Regex.Matches(rule.Declarations, $@"(?:^|;)\s*{Regex.Escape(property)}\s*:\s*([^;]+)", RegexOptions.IgnoreCase)
            .Select(match => Regex.Replace(match.Groups[1].Value, @"\s*!important\s*$", string.Empty, RegexOptions.IgnoreCase).Trim());

    private static bool HasVisibleOutline(CssRule rule)
    {
        static bool VisibleStyle(string value) => Regex.IsMatch(value, @"\b(solid|dashed|dotted|double|groove|ridge|inset|outset)\b", RegexOptions.IgnoreCase);
        static bool Invisible(string value) => Regex.IsMatch(value, @"\b(none|hidden|transparent)\b|(?:^|\s)0(?:px|rem|em)?(?:\s|$)", RegexOptions.IgnoreCase);
        return DeclarationValues(rule, "outline").Any(value => VisibleStyle(value) && !Invisible(value)) ||
            (DeclarationValues(rule, "outline-color").Any(value => !Invisible(value)) &&
             DeclarationValues(rule, "outline-style").Any(VisibleStyle) &&
             DeclarationValues(rule, "outline-width").Any(value => !Invisible(value)));
    }

    private static bool IsScopedAdminDarkSelector(string selector) =>
        selector.StartsWith($"{DarkThemeGate} {AdminBodyGate} ", StringComparison.Ordinal);

    private static bool ContainsComponent(string selector, string component)
    {
        var positiveComponent = component.StartsWith(":not(", StringComparison.Ordinal)
            ? component
            : RemoveNonTargetFunctions(component);
        return Regex.IsMatch(RemoveNonTargetFunctions(selector), Regex.Escape(positiveComponent) + @"(?![a-zA-Z0-9_-])");
    }

    private static string RemoveNonTargetFunctions(string selector)
    {
        // :not excludes a target, and :has describes descendants rather than the styled target.
        for (var i = 0; i < selector.Length; i++)
        {
            if (!selector[i..].StartsWith(":not(", StringComparison.Ordinal) &&
                !selector[i..].StartsWith(":has(", StringComparison.Ordinal)) continue;
            var start = i;
            i = selector.IndexOf('(', i);
            var depth = 1;
            while (++i < selector.Length && depth > 0)
            {
                if (selector[i] == '(') depth++;
                if (selector[i] == ')') depth--;
            }
            selector = selector.Remove(start, i - start);
            i = start - 1;
        }
        return selector;
    }

    private static bool HasDeclaration(CssRule rule, string property, string value) =>
        Regex.IsMatch(rule.Declarations, $@"(?:^|;)\s*{Regex.Escape(property)}\s*:\s*{Regex.Escape(value)}\s*(?:;|$)", RegexOptions.IgnoreCase);

    // Read leaf declarations, including those inside media queries; split lists only outside :is/:has.
    private static IReadOnlyList<CssRule> ReadRules(string styles)
    {
        var rules = new List<CssRule>();
        styles = Regex.Replace(styles, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        foreach (Match match in Regex.Matches(styles, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selectors = match.Groups[1].Value;
            var depth = 0;
            var start = 0;
            for (var i = 0; i <= selectors.Length; i++)
            {
                if (i < selectors.Length && selectors[i] == '(') depth++;
                if (i < selectors.Length && selectors[i] == ')') depth--;
                if (i == selectors.Length || (selectors[i] == ',' && depth == 0))
                {
                    var selector = Regex.Replace(selectors[start..i].Trim(), @"\s+", " ");
                    rules.Add(new CssRule(selector, match.Groups[2].Value));
                    start = i + 1;
                }
            }
        }
        return rules;
    }

    private static string WebProjectPath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    private static string ReadStyles(string filename) => File.ReadAllText(Path.Combine(WebProjectPath(), "wwwroot", "css", filename));

    private static string ReadOrderView(string view) => File.ReadAllText(Path.Combine(WebProjectPath(), "Areas", "Admin", "Views", "Orders", view + ".cshtml"));

    private sealed record CssRule(string Selector, string Declarations);
}
