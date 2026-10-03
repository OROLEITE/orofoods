namespace Orofoods.Web.Tests.Views;

public class AdminUsersViewTests
{
    private static string ProjectPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));

    [Fact]
    public void Index_shows_searchable_user_directory_and_compact_actions()
    {
        var view = ReadView("Index.cshtml");

        Assert.Contains("Usuários", view);
        Assert.Contains("Administre usuários, perfis e permissões de acesso ao sistema.", view);
        Assert.Contains("+ Novo usuário", view);
        Assert.Contains("Buscar por nome ou e-mail", view);
        foreach (var heading in new[] { "Nome", "E-mail", "Perfil", "Cliente", "Vendedor", "Status", "Ações" })
            Assert.Contains($">{heading}<", view);
        Assert.Contains("<details", view);
        Assert.Contains("Redefinir senha", view);
        Assert.Contains("Ativar", view);
        Assert.Contains("Desativar", view);
        Assert.Contains("data-bs-toggle=\"modal\"", view);
        Assert.Contains("Confirmar desativação", view);
        Assert.Contains("Este usuário não poderá acessar o sistema até ser reativado.", view);
    }

    [Fact]
    public void Create_and_edit_views_group_fields_and_keep_antiforgery()
    {
        var create = ReadView("Create.cshtml");
        var edit = ReadView("Edit.cshtml");

        foreach (var section in new[] { "DADOS DO USUÁRIO", "ACESSO", "VÍNCULOS", "SEGURANÇA" })
        {
            Assert.Contains(section, create);
            Assert.Contains(section, edit);
        }
        foreach (var field in new[] { "Name", "Email", "Role", "CustomerId", "SalesRepresentativeId", "Password", "ConfirmPassword", "IsActive" })
        {
            Assert.Contains($"asp-for=\"{field}\"", create);
            if (field is not "Password" and not "ConfirmPassword") Assert.Contains($"asp-for=\"{field}\"", edit);
        }
        Assert.Contains("@Html.AntiForgeryToken()", create);
        Assert.Contains("@Html.AntiForgeryToken()", edit);
    }

    [Fact]
    public void Password_reset_requires_confirmation_and_never_displays_a_token_or_hash()
    {
        var view = ReadView("ResetPassword.cshtml");

        Assert.Contains("ConfirmReset", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("Confirmar redefinição", view);
        Assert.DoesNotContain("PasswordHash", view);
        Assert.DoesNotContain("ResetToken", view);
    }

    [Fact]
    public void User_styles_define_light_dark_and_responsive_states_without_beige_surfaces()
    {
        var styles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "admin-users.css"));

        Assert.Contains("#F8FAFC", styles);
        Assert.Contains("#FFFFFF", styles);
        Assert.Contains("#0F172A", styles);
        Assert.Contains("#2563EB", styles);
        Assert.Contains("[data-theme=\"dark\"]", styles);
        Assert.Contains("@media", styles);
        Assert.DoesNotContain("#F7F4ED", styles, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadView(string name) =>
        File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Admin", "Views", "Users", name));
}
