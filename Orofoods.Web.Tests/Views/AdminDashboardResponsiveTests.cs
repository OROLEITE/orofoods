namespace Orofoods.Web.Tests.Views;

public class AdminDashboardResponsiveTests
{
    [Fact]
    public void Dashboard_grids_fit_desktop_notebook_and_tablet_breakpoints()
    {
        var styles = ReadDashboardStyles();
        var tabletLayout = ExtractMediaBlock(styles, "@media (max-width: 1000px) {");
        var tabletIntegrations = ExtractMediaBlock(styles, "@media (max-width: 1000px) {", useLastOccurrence: true);

        Assert.Contains(".admin-authenticated .admin-dashboard-page .admin-priority-grid {\n    grid-template-columns: repeat(4, minmax(0, 1fr));", styles);
        Assert.Contains(".admin-authenticated .admin-dashboard-page .admin-operation-flow {\n    display: grid;\n    grid-template-columns: repeat(4, minmax(0, 1fr));", styles);
        Assert.Contains(".admin-authenticated .admin-dashboard-page .admin-commercial-metrics {\n    grid-template-columns: repeat(3, minmax(0, 1fr));", styles);
        Assert.Contains(".admin-authenticated .admin-dashboard-page .admin-dashboard-integrations-grid {\n    display: grid;\n    grid-template-columns: repeat(3, minmax(0, 1fr));", styles);

        Assert.Contains(".admin-priority-grid", tabletLayout);
        Assert.Contains(".admin-operation-flow", tabletLayout);
        Assert.Contains(".admin-commercial-metrics", tabletLayout);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", tabletLayout);
        Assert.Contains(".admin-dashboard-integrations-grid", tabletIntegrations);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", tabletIntegrations);
    }

    [Fact]
    public void Dashboard_stacks_hero_and_content_grids_on_mobile()
    {
        var styles = ReadDashboardStyles();
        var heroMobile = ExtractMediaBlock(styles, "@media (max-width: 767px) {");
        var contentMobile = ExtractMediaBlock(styles, "@media (max-width: 650px) {");
        var integrationsMobile = ExtractMediaBlock(styles, "@media (max-width: 650px) {", useLastOccurrence: true);

        Assert.Contains(".admin-dashboard-hero .container", heroMobile);
        Assert.Contains("min-height: 0;", heroMobile);
        Assert.Contains("flex-direction: column;", heroMobile);
        Assert.Contains(".admin-dashboard-hero aside", heroMobile);
        Assert.Contains("width: 100%;", heroMobile);
        Assert.Contains("max-width: none;", heroMobile);

        Assert.Contains(".admin-priority-grid,", contentMobile);
        Assert.Contains(".admin-operation-flow,", contentMobile);
        Assert.Contains(".admin-commercial-metrics", contentMobile);
        Assert.Contains("grid-template-columns: 1fr;", contentMobile);
        Assert.Contains(".admin-dashboard-integrations-grid", integrationsMobile);
        Assert.Contains("grid-template-columns: 1fr;", integrationsMobile);
    }

    [Fact]
    public void Dashboard_cards_keep_content_width_accessibility_and_textual_statuses()
    {
        var styles = ReadDashboardStyles();
        var view = ReadDashboardView();

        Assert.Contains(".admin-authenticated .admin-dashboard-page .admin-operation-section {\n    min-width: 0;", styles);
        Assert.Contains(".admin-authenticated .admin-dashboard-page .admin-operation-step,\n.admin-authenticated .admin-dashboard-page .admin-operation-metrics article {\n    min-width: 0;", styles);
        Assert.Contains(".admin-dashboard-integration-card {\n    display: flex;\n    min-width: 0;", styles);
        Assert.Contains(".admin-recent-orders:not(.admin-dashboard-integrations) {\n    min-width: 0;", styles);
        Assert.Contains("<div class=\"table-responsive\">", view);

        Assert.Contains(".admin-priority-card:hover", styles);
        Assert.Contains(".admin-priority-card:focus-visible", styles);
        Assert.Contains(".admin-orders-table a:focus-visible", styles);
        Assert.Contains(".admin-dashboard-integration-card nav a:hover", styles);
        Assert.Contains(".admin-dashboard-integration-card nav a:focus-visible", styles);
        Assert.Contains("Falha registrada na &uacute;ltima sincroniza&ccedil;&atilde;o", view);
        Assert.Contains("Sincroniza&ccedil;&atilde;o em andamento", view);
        Assert.Contains("&Uacute;ltima sincroniza&ccedil;&atilde;o conclu&iacute;da", view);
        Assert.Contains("N&atilde;o monitorado", view);
    }

    private static string ExtractMediaBlock(string styles, string marker, bool useLastOccurrence = false)
    {
        var start = useLastOccurrence
            ? styles.LastIndexOf(marker, StringComparison.Ordinal)
            : styles.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing responsive rule: {marker}");

        var openingBrace = styles.IndexOf('{', start);
        var depth = 0;
        for (var index = openingBrace; index < styles.Length; index++)
        {
            if (styles[index] == '{') depth++;
            if (styles[index] == '}' && --depth == 0)
            {
                return styles[start..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Unclosed responsive rule: {marker}");
    }

    private static string ReadDashboardStyles() =>
        File.ReadAllText(Path.Combine(ProjectPath(), "wwwroot", "css", "admin-dashboard.css"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string ReadDashboardView() =>
        File.ReadAllText(Path.Combine(ProjectPath(), "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));

    private static string ProjectPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
}
