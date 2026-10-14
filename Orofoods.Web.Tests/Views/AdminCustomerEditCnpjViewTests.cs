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
}
