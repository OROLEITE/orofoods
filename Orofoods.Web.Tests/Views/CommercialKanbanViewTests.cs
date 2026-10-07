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
}
