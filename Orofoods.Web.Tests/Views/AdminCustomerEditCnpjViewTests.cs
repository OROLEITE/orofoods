namespace Orofoods.Web.Tests.Views;

public sealed class AdminCustomerEditCnpjViewTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Customer_edit_exposes_cnpj_correction_and_reason_only_to_admins()
    {
        var view = File.ReadAllText(Path.Combine(ProjectPath, "Areas/Admin/Views/Customers/Edit.cshtml"));
        var controller = File.ReadAllText(Path.Combine(ProjectPath, "Areas/Admin/Controllers/CustomersController.cs"));

        Assert.Contains("asp-for=\"Cnpj\"", view);
        Assert.Contains("asp-for=\"CnpjChangeReason\"", view);
        Assert.Contains("asp-validation-summary=\"All\"", view);
        Assert.Contains("User.IsInRole(\"Administrador\")", view);
        Assert.Contains("[Authorize(Roles = \"Administrador\")]", controller);
    }

    [Fact]
    public void Customer_edit_separates_pending_approval_from_commercial_configuration()
    {
        var view = File.ReadAllText(Path.Combine(ProjectPath, "Areas/Admin/Views/Customers/Edit.cshtml"));
        var approvalButtonIndex = view.IndexOf("name=\"approve\" value=\"true\"", StringComparison.Ordinal);
        var pendingBranchIndex = view.LastIndexOf("@if (isPending)", approvalButtonIndex, StringComparison.Ordinal);

        Assert.Contains("var isPending = Model.Status == CustomerStatus.Pending;", view);
        Assert.Contains("Configurações comerciais", view);
        Assert.Contains("Salvar alterações", view);
        Assert.DoesNotContain("Aprovar e salvar", view);
        Assert.True(pendingBranchIndex >= 0 && pendingBranchIndex < approvalButtonIndex);
        Assert.DoesNotContain("<main class=\"customer-approval-form\">", view);
    }

    [Fact]
    public void Customer_edit_uses_admin_scoped_responsive_theme_styles()
    {
        var layout = File.ReadAllText(Path.Combine(ProjectPath, "Views/Shared/_AdminLayout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot/css/admin-customer-config.css"));

        Assert.Contains("admin-customer-config.css", layout);
        Assert.Contains("Values[\"action\"]?.ToString() == \"Edit\"", layout);
        Assert.Contains(".admin-authenticated .customer-approval-page", styles);
        Assert.Contains("@media (max-width: 767.98px)", styles);
        Assert.Contains("html[data-theme=\"dark\"] body.admin-authenticated .customer-approval-page", styles);
    }
}
