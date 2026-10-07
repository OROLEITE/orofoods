using Orofoods.Web.Areas.Admin.Controllers;

namespace Orofoods.Web.Tests.Views;

public class AdminDashboardIntegrationsViewTests
{
    [Fact]
    public void Integrations_section_follows_monthly_result_and_precedes_recent_orders_with_existing_routes()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "admin-dashboard.css"));
        var dashboardController = File.ReadAllText(Path.Combine(projectPath, "Areas", "Admin", "Controllers", "DashboardController.cs"));
        var sectionIndex = view.IndexOf("admin-dashboard-integrations", StringComparison.Ordinal);
        var sectionEnd = sectionIndex >= 0
            ? view.IndexOf("</section>", sectionIndex, StringComparison.Ordinal)
            : -1;
        var integrations = sectionIndex >= 0 && sectionEnd > sectionIndex
            ? view[sectionIndex..sectionEnd]
            : string.Empty;

        Assert.True(sectionIndex >= 0);
        Assert.True(view.IndexOf("Resultado do m&ecirc;s", StringComparison.Ordinal) < sectionIndex);
        Assert.True(sectionIndex < view.IndexOf("Pedidos recentes", sectionIndex, StringComparison.Ordinal));
        Assert.Contains("WmcSyncIsRunning", integrations);
        Assert.Contains("WmcSyncHasRun", integrations);
        Assert.Contains("WmcSyncHasFailed", integrations);
        Assert.Contains("ActiveMercadoPagoPointTerminals", integrations);
        Assert.Contains("FailedWhatsAppMessages", integrations);
        Assert.DoesNotContain("FailedWmcExports", integrations);
        Assert.DoesNotContain("FailedIntegrations", integrations);
        Assert.Contains("asp-controller=\"Integrations\" asp-action=\"Wmc\"", integrations);
        Assert.Contains("asp-controller=\"DriverPaymentTerminalAssignments\" asp-action=\"Index\"", integrations);
        Assert.Contains("asp-controller=\"WhatsApp\" asp-action=\"Index\"", integrations);
        Assert.Equal(3, CountOccurrences(integrations, "class=\"admin-dashboard-integration-card\""));
        Assert.DoesNotContain("Normal", integrations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N&atilde;o monitorado", integrations);
        Assert.Contains("Sem execu&ccedil;&atilde;o registrada nesta inst&acirc;ncia", integrations);
        Assert.Contains("html[data-theme=\"dark\"] .admin-dashboard-page .admin-dashboard-integration-card", styles);

        Assert.DoesNotContain("RunExclusivelyAsync", dashboardController);
        Assert.DoesNotContain("SyncAllAsync", dashboardController);
        Assert.DoesNotContain("SendAsync", dashboardController);
        Assert.DoesNotContain("HttpClient", dashboardController);
        Assert.NotNull(typeof(IntegrationsController).GetMethod(nameof(IntegrationsController.Wmc)));
        Assert.NotNull(typeof(DriverPaymentTerminalAssignmentsController).GetMethod(nameof(DriverPaymentTerminalAssignmentsController.Index)));
        Assert.NotNull(typeof(WhatsAppController).GetMethod(nameof(WhatsAppController.Index)));
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
