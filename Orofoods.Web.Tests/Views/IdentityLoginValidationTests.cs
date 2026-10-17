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
    public void Login_primary_action_matches_the_client_access_reference_and_is_accessible()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("id=\"login-submit\" type=\"submit\" class=\"btn btn-gold auth-submit\">Logar no sistema", login);
        Assert.Contains("<i class=\"fa-solid fa-arrow-right\" aria-hidden=\"true\"></i></button>", login);
        Assert.Contains(".auth-submit{display:flex;justify-content:space-between;align-items:center;width:100%", styles);
        Assert.Contains(".auth-screen #login-submit.auth-submit {", styles);
        Assert.Contains("min-height: 54px;", styles);
        Assert.Contains("background: linear-gradient(100deg, #e8b42e 0%, #d99a1d 100%);", styles);
        Assert.Contains(".btn-gold{background:var(--gold);border-color:var(--gold);color:#17130b}", styles);
        Assert.Contains("border-radius: 50%;", styles);
        Assert.Contains("background: #b97812;", styles);
        Assert.Contains("color: #fff;", styles);
        Assert.Contains(".auth-screen #login-submit.auth-submit:hover {", styles);
        Assert.Contains(".auth-screen #login-submit.auth-submit:focus-visible {", styles);
        Assert.Contains("outline: 3px solid #80570e;", styles);
        Assert.Contains("@media(max-width:900px){.auth-screen{overflow:auto}", styles);
        Assert.Contains("@media(max-width:600px){.auth-layout{padding:28px 16px}", styles);

        Assert.True(ContrastRatio("17130b", "e8b42e") >= 4.5);
        Assert.True(ContrastRatio("17130b", "d99a1d") >= 4.5);
        Assert.True(ContrastRatio("ffffff", "b97812") >= 3);
        Assert.True(ContrastRatio("80570e", "ffffff") >= 4.5);
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

    private static double ContrastRatio(string foreground, string background)
    {
        var first = RelativeLuminance(foreground);
        var second = RelativeLuminance(background);
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string color)
    {
        var red = Convert.ToInt32(color[..2], 16) / 255d;
        var green = Convert.ToInt32(color.Substring(2, 2), 16) / 255d;
        var blue = Convert.ToInt32(color.Substring(4, 2), 16) / 255d;

        static double Linearize(double channel) => channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);

        return (0.2126 * Linearize(red)) + (0.7152 * Linearize(green)) + (0.0722 * Linearize(blue));
    }
}
