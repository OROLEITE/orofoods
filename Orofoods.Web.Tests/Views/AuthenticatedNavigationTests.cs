namespace Orofoods.Web.Tests.Views;

public class AuthenticatedNavigationTests
{
    [Fact]
    public void Shared_layout_switches_public_actions_for_authenticated_users()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("User.Identity?.IsAuthenticated", layout);
        Assert.Contains("User.IsInRole(\"Administrador\")", layout);
        Assert.Contains("asp-area=\"Admin\" asp-controller=\"Dashboard\" asp-action=\"Index\"", layout);
        Assert.Contains("Sair", layout);
        Assert.Contains("/Account/Logout", layout);
    }
}
