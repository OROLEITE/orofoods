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
        Assert.Contains("Buscar por nome, e-mail ou login", view);
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
    public void Actions_menu_keeps_existing_links_and_is_visible_when_open()
    {
        var view = ReadView("Index.cshtml");
        var styles = File.ReadAllText(Path.Combine(ProjectPath, "wwwroot", "css", "admin-users.css"));

        Assert.Contains("<a asp-action=\"History\"", view);
        Assert.Contains("<a asp-action=\"Edit\"", view);
        Assert.Contains("<a asp-action=\"ResetPassword\"", view);
        Assert.Contains(".admin-users-table-wrap:has(.admin-users-actions[open])", styles);
        Assert.Contains(".admin-table-card:has(.admin-users-actions[open])", styles);
        Assert.Contains(".admin-users-table td:has(.admin-users-actions[open])", styles);
        Assert.Contains(".admin-users-actions summary:focus-visible", styles);
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
    public void User_access_pages_separate_login_show_temporary_password_controls_and_history()
    {
        var index = ReadView("Index.cshtml");
        var create = ReadView("Create.cshtml");
        var edit = ReadView("Edit.cshtml");
        var reset = ReadView("ResetPassword.cshtml");
        var history = ReadView("History.cshtml");
        var navigation = File.ReadAllText(Path.Combine(ProjectPath, "Views", "Shared", "_AdminNavigation.cshtml"));

        Assert.Contains("FullName", index);
        Assert.Contains("UserName", index);
        Assert.Contains("asp-for=\"UserName\"", create);
        Assert.Contains("asp-for=\"UserName\"", edit);
        Assert.Contains("asp-for=\"MustChangePassword\"", create);
        Assert.Contains("asp-for=\"MustChangePassword\"", reset);
        Assert.Contains("asp-action=\"History\"", index);
        Assert.Contains("Alterações registradas", history);
        Assert.DoesNotContain("PasswordHash", history);
        Assert.DoesNotContain("ResetToken", history);
        Assert.Contains("Configurações", navigation);
        Assert.Contains("Usuários e Acessos", navigation);
        Assert.DoesNotContain("PasswordHash",
            typeof(Orofoods.Web.ViewModels.AdminUserRow).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Forced_password_change_view_is_antiforgery_protected_and_has_no_secret_output()
    {
        var view = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Identity", "Views", "Account", "ChangeTemporaryPassword.cshtml"));
        var imports = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Identity", "Views", "_ViewImports.cshtml"));
        var start = File.ReadAllText(Path.Combine(ProjectPath, "Areas", "Identity", "Views", "_ViewStart.cshtml"));

        Assert.Contains("CurrentPassword", view);
        Assert.Contains("NewPassword", view);
        Assert.Contains("ConfirmNewPassword", view);
        Assert.Contains("@Html.AntiForgeryToken()", view);
        Assert.Contains("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers", imports);
        Assert.Contains("/Views/Shared/_Layout.cshtml", start);
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
