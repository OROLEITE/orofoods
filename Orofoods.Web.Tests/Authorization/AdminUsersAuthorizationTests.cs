using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Orofoods.Web.Areas.Admin.Controllers;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Authorization;

public class AdminUsersAuthorizationTests
{
    [Fact]
    public void User_management_controller_is_administrator_only()
    {
        var authorize = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal("Administrador", authorize?.Roles);
    }

    [Fact]
    public void User_listing_accepts_search_role_and_status_filters()
    {
        var index = typeof(UsersController).GetMethod(nameof(UsersController.Index));

        Assert.NotNull(index);
        Assert.Equal(new[] { "query", "role", "isActive" }, index!.GetParameters().Select(x => x.Name));
        Assert.Equal(typeof(string), index.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(string), index.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(bool?), index.GetParameters()[2].ParameterType);
    }

    [Fact]
    public async Task Index_filters_by_search_role_and_active_state()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userManager = TestIdentityFactory.CreateUserManager(db);
        var roleManager = TestIdentityFactory.CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole("Operador"));
        await roleManager.CreateAsync(new IdentityRole("Cliente"));
        await CreateUserAsync(userManager, "Operador do SAC", "sac@test.local", "Operador", true);
        await CreateUserAsync(userManager, "Operador inativo", "inactive@test.local", "Operador", false);
        await CreateUserAsync(userManager, "Cliente SAC", "customer@test.local", "Cliente", true);
        var controller = new UsersController(db, userManager, roleManager, new AdminUserService(db, userManager, roleManager));
        var index = typeof(UsersController).GetMethod(nameof(UsersController.Index))!;

        var action = (Task<IActionResult>)index.Invoke(controller, ["sac", "Operador", true])!;
        var view = Assert.IsType<ViewResult>(await action);
        var model = Assert.IsAssignableFrom<object>(view.Model);
        var rows = ((IEnumerable)model.GetType().GetProperty("Users")!.GetValue(model)!).Cast<object>().ToList();

        var row = Assert.Single(rows);
        Assert.Equal("Operador do SAC", ((ApplicationUser)row.GetType().GetProperty("User")!.GetValue(row)!).UserName);
        Assert.Equal("Operador", row.GetType().GetProperty("Role")!.GetValue(row));
    }

    [Fact]
    public void Every_user_mutation_is_post_only_and_antiforgery_protected()
    {
        foreach (var actionName in new[] { "Create", "Edit", "Activate", "Deactivate", "ResetPassword" })
        {
            var postAction = typeof(UsersController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.Name == actionName)
                .FirstOrDefault(method => method.GetCustomAttribute<HttpPostAttribute>() is not null);

            Assert.NotNull(postAction);
            Assert.NotNull(postAction!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    private static async Task CreateUserAsync(UserManager<ApplicationUser> userManager, string name, string email, string role, bool active)
    {
        var user = new ApplicationUser { UserName = name, Email = email, IsActive = active };
        var create = await userManager.CreateAsync(user, "Valid!Pass123");
        Assert.True(create.Succeeded, string.Join("; ", create.Errors.Select(x => x.Description)));
        var addRole = await userManager.AddToRoleAsync(user, role);
        Assert.True(addRole.Succeeded);
    }
}
