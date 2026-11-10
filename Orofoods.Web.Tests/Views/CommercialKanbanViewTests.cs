using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Views;

public class CommercialKanbanViewTests
{
    [Fact]
    public void Commercial_navigation_has_an_active_overview_link_to_the_existing_route()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var navigation = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_AdminNavigation.cshtml"));

        Assert.Contains("<span>Visão geral</span>", navigation);
        Assert.Contains("controller == \"Commercial\" && action == \"Index\"", navigation);
        Assert.Matches("asp-controller=\"Commercial\" asp-action=\"Index\"[^>]*><i[^>]+></i><span>Visão geral</span>", navigation);
    }

    [Fact]
    public void Commercial_kanban_precedes_today_and_routine_without_duplication()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Commercial", "Index.cshtml"));

        var kanban = view.IndexOf("Kanban de atendimentos", StringComparison.Ordinal);
        var heroEnd = view.IndexOf("</header>", view.IndexOf("commercial-dashboard-hero", StringComparison.Ordinal), StringComparison.Ordinal);
        var today = view.IndexOf("commercial-overview-today", StringComparison.Ordinal);
        var routine = view.IndexOf("Minha rotina", StringComparison.Ordinal);

        Assert.True(heroEnd >= 0 && heroEnd < kanban);
        Assert.True(kanban >= 0 && kanban < today && today < routine);
        Assert.Equal(1, view.Split("Kanban de atendimentos", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Commercial_kanban_markup_posts_to_the_antiforgery_protected_existing_action()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Commercial", "Index.cshtml"));

        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("asp-action=\"CompleteActivity\"", view);
        Assert.Contains("data-current-status", view);
        Assert.Contains("draggable=\"false\"", view);
    }

    [Fact]
    public void Kanban_drag_is_limited_to_mouse_and_has_page_scoped_visual_feedback()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "commercial-kanban.js"));
        var css = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "commercial.css"));

        Assert.Contains("(hover: hover) and (pointer: fine)", script);
        Assert.Contains("card.classList.add(\"is-dragging\")", script);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .commercial-dashboard-page .commercial-activity-card.is-dragging", css);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated:not(:has(.whatsapp-inbox-page)) .commercial-dashboard-page .commercial-kanban-column.is-drop-target", css);
        Assert.DoesNotContain("whatsapp", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Kanban_cards_keep_compact_hierarchy_and_truncate_long_text_without_changing_drag_endpoint()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Commercial", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "js", "commercial-kanban.js"));
        var css = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "commercial-kanban-cards.css"));
        var cardStart = view.IndexOf("class=\"commercial-activity-card", StringComparison.Ordinal);
        var cardEnd = view.IndexOf("</a>", cardStart, StringComparison.Ordinal);
        var cardMarkup = view[cardStart..cardEnd];

        Assert.Contains("commercial-activity-meta", cardMarkup);
        Assert.Contains("commercial-activity-customer", cardMarkup);
        Assert.Contains("commercial-activity-subject", cardMarkup);
        Assert.Contains("commercial-activity-person", cardMarkup);
        Assert.Contains("commercial-activity-action", cardMarkup);
        Assert.Contains("title=\"Atendimento: @(activity.AssignedUser?.Email", cardMarkup);
        AssertRuleHas(css, ".commercial-dashboard-page .commercial-activity-card .commercial-activity-customer", "-webkit-line-clamp", "2");
        AssertRuleHas(css, ".commercial-dashboard-page .commercial-activity-card .commercial-activity-subject", "-webkit-line-clamp", "2");
        AssertRuleHas(css, ".commercial-dashboard-page .commercial-activity-card .commercial-activity-person", "text-overflow", "ellipsis");
        Assert.Contains("statusForm.action", script);
        Assert.Contains("asp-action=\"CompleteActivity\"", view);
        Assert.Contains("commercial-kanban.js", view);
    }

    [Fact]
    public void Commercial_empty_states_and_attention_panel_keep_content_driven_compact_spacing()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var css = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "commercial.css"));
        const string scope = ".commercial-dashboard-page ";

        AssertRuleHas(css, scope + ".commercial-routine-list--overview>.commercial-empty-column:only-child", "margin", "0");
        AssertRuleHas(css, scope + ".commercial-routine-list--overview>.commercial-empty-column:only-child", "padding", "12px 4px 14px");
        AssertRuleHas(css, scope + ".commercial-agenda-panel>.commercial-empty-column:last-child", "margin", "0");
        AssertRuleHas(css, scope + ".commercial-agenda-panel>.commercial-empty-column:last-child", "padding", "12px 4px 4px");
        AssertRuleHas(css, scope + ".commercial-attention-panel", "padding", "16px 18px");
        AssertRuleHas(css, scope + ".commercial-attention-panel .commercial-panel-heading", "margin-bottom", "12px");
        AssertRuleHas(css, scope + ".commercial-attention-grid", "gap", "8px");
        AssertRuleHas(css, scope + ".commercial-attention-card", "padding", "12px 14px");
        AssertRuleHas(css, scope + ".commercial-attention-panel>.commercial-routine-list>.commercial-empty-column:only-child", "padding", "12px 4px 0");

        foreach (var selector in new[]
                 {
                     scope + ".commercial-routine-list--overview>.commercial-empty-column:only-child",
                     scope + ".commercial-agenda-panel>.commercial-empty-column:last-child",
                     scope + ".commercial-attention-panel>.commercial-routine-list>.commercial-empty-column:only-child"
                 })
        {
            var declarations = RuleDeclarations(css, selector);
            Assert.DoesNotMatch(@"(?:^|;)\s*(?:height|max-height|min-height)\s*:", declarations);
        }
    }

    [Fact]
    public void Commercial_overview_uses_status_colors_compact_routine_header_and_balanced_daily_metrics()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Commercial", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard-indicators.css"));
        var routineHeaderStart = view.IndexOf("<header class=\"commercial-panel-heading commercial-routine-heading\"", StringComparison.Ordinal);
        var routineHeaderEnd = routineHeaderStart < 0 ? -1 : view.IndexOf("</header>", routineHeaderStart, StringComparison.Ordinal);

        Assert.Contains("Model.Attention.OverdueReturns == 0 ? \"admin-dashboard-indicator--normal\" : \"admin-dashboard-indicator--warning\"", view);
        Assert.Contains("Model.Attention.CustomersWithoutPurchase == 0 ? \"admin-dashboard-indicator--normal\" : \"admin-dashboard-indicator--warning\"", view);
        Assert.DoesNotContain("admin-dashboard-indicator--critical", view);
        Assert.Contains("@Model.OrdersToday", view);
        Assert.Contains("@Model.TodayRevenue.ToString(\"C\")", view);
        Assert.Contains("asp-route-id=\"@item.CustomerId\"", view);
        Assert.True(routineHeaderStart >= 0 && routineHeaderEnd > routineHeaderStart);
        var routineHeader = view[routineHeaderStart..routineHeaderEnd];
        Assert.Contains("commercial-priority-legend", routineHeader);
        Assert.Equal(1, view.Split("commercial-priority-legend", StringSplitOptions.None).Length - 1);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", styles);
        Assert.Contains("commercial-routine-list--overview", styles);
        Assert.Contains("commercial-routine-heading-meta", styles);
        AssertRuleHas(styles, ".admin-authenticated .admin-shell .commercial-dashboard-page .commercial-routine-panel .commercial-panel-heading", "padding", "12px 18px");
        AssertRuleHas(styles, ".admin-authenticated .admin-shell .commercial-dashboard-page .commercial-routine-list--overview .commercial-routine-item", "padding", "10px 0");
        AssertRuleHas(styles, ".admin-authenticated .admin-shell .commercial-dashboard-page .commercial-metric-grid--secondary", "grid-template-columns", "repeat(2, minmax(0, 1fr))");
        Assert.Contains("priority-dot--urgent", styles);
        Assert.Contains("color: #F59E0B;", styles);
        Assert.Contains("color: #38BDF8;", styles);
        Assert.Contains("@media (max-width: 520px)", styles);
    }

    private static void AssertRuleHas(string css, string selector, string property, string value)
    {
        var declarations = RuleDeclarations(css, selector);
        Assert.Matches($@"(?:^|;)\s*{Regex.Escape(property)}\s*:\s*{Regex.Escape(value)}\s*(?:;|$)", declarations);
    }

    private static string RuleDeclarations(string css, string selector)
    {
        var match = Regex.Match(css, Regex.Escape(selector) + @"\s*\{(?<declarations>[^}]*)\}");
        Assert.True(match.Success, $"Missing CSS rule for {selector}.");
        return match.Groups["declarations"].Value;
    }
}
