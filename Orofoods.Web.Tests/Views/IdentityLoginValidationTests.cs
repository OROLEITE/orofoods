using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Areas.Identity.Pages.Account;

namespace Orofoods.Web.Tests.Views;

public class IdentityLoginValidationTests
{
    [Fact]
    public void Login_view_uses_font_awesome_for_navigation_actions()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));

        Assert.Contains("fa-arrow-right", login);
        Assert.Contains("fa-arrow-left", login);
    }

    [Fact]
    public void Login_home_links_target_the_public_area()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));

        Assert.Equal(2, login.Split("asp-area=\"\" asp-controller=\"Home\" asp-action=\"Index\"").Length - 1);
    }

    [Fact]
    public void Login_view_marks_the_page_as_a_viewport_sized_auth_screen()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));
        var layout = File.ReadAllText(Path.Combine(projectPath, "Views", "Shared", "_Layout.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("BodyClass", login);
        Assert.Contains("BodyClass", layout);
        Assert.Contains(".auth-screen .site-footer{display:none}", styles);
    }

    [Fact]
    public void Login_view_uses_the_institutional_operation_photo_instead_of_the_logo()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));

        Assert.Contains("orofoods-institutional-kitchen.png", login);
        Assert.Contains("auth-operation-photo", login);
    }

    [Fact]
    public void Login_operation_photo_has_a_prominent_desktop_size()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains(".auth-operation-photo{display:block;width:520px;height:320px", styles);
    }

    [Fact]
    public void Login_operation_photo_remains_prominent_on_short_desktop_viewports()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("@media(max-height:720px) and (min-width:901px){.auth-operation-photo{width:420px;height:240px}", styles);
    }

    [Fact]
    public void Empty_credentials_show_portuguese_required_messages()
    {
        var input = new LoginModel.InputModel();
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);

        Assert.Contains(results, result => result.ErrorMessage == "O e-mail corporativo \u00e9 obrigat\u00f3rio.");
        Assert.Contains(results, result => result.ErrorMessage == "A senha \u00e9 obrigat\u00f3ria.");
    }

    [Fact]
    public void Invalid_email_shows_a_portuguese_message()
    {
        var input = new LoginModel.InputModel { Email = "invalido", Password = "Senha123!" };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);

        Assert.Contains(results, result => result.ErrorMessage == "Informe um e-mail corporativo v\u00e1lido.");
    }
}
