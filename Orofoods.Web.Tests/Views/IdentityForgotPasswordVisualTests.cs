namespace Orofoods.Web.Tests.Views;

public sealed class IdentityForgotPasswordVisualTests
{
    private static string WebProjectPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Forgot_password_view_uses_identity_package_model_and_validated_email_post()
    {
        var view = Read("Areas", "Identity", "Pages", "Account", "ForgotPassword.cshtml");

        Assert.Contains("@model Microsoft.AspNetCore.Identity.UI.V5.Pages.Account.Internal.ForgotPasswordModel", view);
        Assert.Contains("<form method=\"post\"", view);
        Assert.Contains("asp-for=\"Input.Email\"", view);
        Assert.Contains("autocomplete=\"email\"", view);
        Assert.Contains("asp-validation-summary=\"ModelOnly\"", view);
        Assert.Contains("asp-validation-for=\"Input.Email\"", view);
        Assert.False(File.Exists(Path.Combine(WebProjectPath, "Areas", "Identity", "Pages", "Account", "ForgotPassword.cshtml.cs")));
    }

    [Fact]
    public void Forgot_password_view_uses_responsive_auth_components_pt_br_copy_and_site_footer()
    {
        var view = Read("Areas", "Identity", "Pages", "Account", "ForgotPassword.cshtml");
        var styles = Read("wwwroot", "css", "customer-experience.css");
        var layout = Read("Views", "Shared", "_Layout.cshtml");

        Assert.Contains("identity-recovery-page", view);
        Assert.Contains("auth-layout", view);
        Assert.Contains("auth-card", view);
        Assert.Contains("auth-form", view);
        Assert.Contains("Recuperar senha", view);
        Assert.Contains("e-mail cadastrado", view);
        Assert.Contains("Voltar ao login", view);
        Assert.DoesNotContain("auth-screen", view);
        Assert.Contains("identity-recovery-page", styles);
        Assert.Contains("@media (max-width: 575.98px)", styles);
        Assert.Contains("<footer class=\"site-footer\"", layout);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { WebProjectPath }.Concat(parts).ToArray()));
}
