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
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "client-login.css"));

        Assert.Contains("id=\"login-submit\" type=\"submit\" class=\"btn btn-gold client-login-submit\">Entrar no Portal do Cliente", login);
        Assert.Contains("<i class=\"fa-solid fa-arrow-right\" aria-hidden=\"true\"></i></button>", login);
        Assert.Contains(".client-login-submit {", styles);
        Assert.Contains("min-height: 52px;", styles);
        Assert.Contains("background: var(--client-login-gold);", styles);
        Assert.Contains("--client-login-gold: #D49B24;", styles);
        Assert.Contains("--client-login-gold-hover: #B98212;", styles);
        Assert.Contains("--client-login-ink: #0F172A;", styles);
        Assert.Contains("border-radius: 50%;", styles);
        Assert.Contains(".client-login-submit:hover {", styles);
        Assert.Contains(".client-login-submit:focus-visible {", styles);
        Assert.Contains("outline: 3px solid rgb(212 155 36 / 55%);", styles);
        Assert.Contains("@media (max-width: 820px)", styles);
        Assert.Contains("@media (max-width: 600px)", styles);

        Assert.True(ContrastRatio("0F172A", "D49B24") >= 4.5);
        Assert.True(ContrastRatio("0F172A", "B98212") >= 4.5);
        Assert.True(ContrastRatio("80570e", "ffffff") >= 4.5);
    }

    [Fact]
    public void Login_home_links_target_the_public_area()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));

        Assert.Equal(1, login.Split("asp-area=\"\" asp-controller=\"Home\" asp-action=\"Index\"").Length - 1);
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
    public void Login_hero_uses_a_local_kitchen_background_and_preserves_identity_controls()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "client-login.css"));

        Assert.Contains("orofoods-institutional-kitchen.png", styles);
        Assert.True(File.Exists(Path.Combine(projectPath, "wwwroot", "images", "orofoods-institutional-kitchen.png")));
        Assert.Contains("asp-for=\"Input.Email\"", login);
        Assert.Contains("asp-for=\"Input.Password\"", login);
        Assert.Contains("asp-for=\"Input.RememberMe\"", login);
        Assert.Contains("data-password-toggle=\"Input_Password\"", login);
        Assert.Contains("asp-page=\"./ForgotPassword\"", login);
        Assert.Contains("asp-controller=\"CustomerRegistration\" asp-action=\"Register\"", login);
    }

    [Fact]
    public void Login_hero_background_covers_the_main_section_with_a_dark_left_gradient()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "client-login.css"));

        Assert.Contains(".client-login-page {", styles);
        Assert.Contains("linear-gradient(90deg, rgb(33 26 20 / 96%) 0%,", styles);
        Assert.Contains("url(\"/images/orofoods-institutional-kitchen.png\") center 57% / cover;", styles);
    }

    [Fact]
    public void Login_has_the_requested_presentational_and_trust_benefits()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        var login = File.ReadAllText(Path.Combine(projectPath, "Areas", "Identity", "Pages", "Account", "Login.cshtml"));
        var styles = File.ReadAllText(Path.Combine(projectPath, "wwwroot", "css", "site.css"));

        Assert.Contains("Pedidos online", login);
        Assert.Contains("Produtos sempre atualizados", login);
        Assert.Contains("Condi&ccedil;&otilde;es comerciais", login);
        Assert.Contains("Compra B2B", login);
        Assert.Contains("Atendimento especializado", login);
        Assert.Contains("Suporte dedicado", login);
        Assert.Contains("fa-cart-shopping", login);
        Assert.Contains("fa-box-open", login);
        Assert.Contains("fa-chart-column", login);
        Assert.Contains("client-login-trust", login);
        Assert.Contains(".auth-screen .site-footer{display:none}", styles);
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
