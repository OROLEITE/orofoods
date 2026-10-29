using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Orofoods.Web.Areas.Admin.Controllers;

namespace Orofoods.Web.Tests.Views;

public sealed class WmcActivationDiagnosticsViewTests
{
    [Fact]
    public void Wmc_view_invokes_the_read_only_activation_diagnostics_component()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Integrations", "Wmc.cshtml"));

        Assert.Contains("@await Component.InvokeAsync(\"WmcActivationDiagnostics\")", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostics_view_displays_only_aggregates_and_activation_flags()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var view = File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Shared", "Components", "WmcActivationDiagnostics", "Default.cshtml"));

        Assert.Contains("@Model.MaxOrderId", view, StringComparison.Ordinal);
        Assert.Contains("@Model.TotalOrderCount", view, StringComparison.Ordinal);
        Assert.Contains("AutomaticWorkerStartAfterOrderId", view, StringComparison.Ordinal);
        Assert.Contains("@Model.AutomaticWorkerState", view, StringComparison.Ordinal);
        Assert.Contains("WmcJsonOrderEnabled", view, StringComparison.Ordinal);
        Assert.Contains("AutomaticWorkerEnabled", view, StringComparison.Ordinal);
        Assert.Contains("WmcFileDropEnabled", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Último ciclo", view, StringComparison.Ordinal);
        Assert.Contains("Serviço hospedado", view, StringComparison.Ordinal);
        Assert.Contains("Telemetria em memória desta instância", view, StringComparison.Ordinal);
        Assert.Contains("diagnosticOrderId", view, StringComparison.Ordinal);
        Assert.Contains("motivos", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("processados", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@lastCycle.FailedCount", view, StringComparison.Ordinal);
        Assert.Contains("@lastCycle.CycleErrorCount", view, StringComparison.Ordinal);
        Assert.DoesNotContain("lastCycle.FailedCount + lastCycle.CycleErrorCount", view, StringComparison.Ordinal);
        Assert.Contains("Ignorados antes da reserva", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Wmc_action_remains_administrator_only_without_allow_anonymous_override()
    {
        var controllerAuthorization = typeof(IntegrationsController).GetCustomAttribute<AuthorizeAttribute>();
        var action = typeof(IntegrationsController).GetMethod(nameof(IntegrationsController.Wmc));

        Assert.NotNull(controllerAuthorization);
        Assert.Equal("Administrador", controllerAuthorization.Roles);
        Assert.NotNull(action);
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
