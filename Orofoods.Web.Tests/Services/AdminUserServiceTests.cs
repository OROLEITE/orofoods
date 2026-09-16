using Microsoft.AspNetCore.Identity;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class AdminUserServiceTests
{
    [Fact]
    public async Task Updates_active_state_and_replaces_role()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userManager = TestIdentityFactory.CreateUserManager(db);
        var roleManager = TestIdentityFactory.CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole("Cliente"));
        await roleManager.CreateAsync(new IdentityRole("Administrador"));
        var user = new ApplicationUser { UserName = "user@test", Email = "user@test", IsActive = true };
        await userManager.CreateAsync(user);
        await userManager.AddToRoleAsync(user, "Cliente");
        var originalSecurityStamp = await userManager.GetSecurityStampAsync(user);
        var sut = new AdminUserService(userManager, roleManager);

        await sut.UpdateAsync(user.Id, false, null, null, "Administrador");

        Assert.False(user.IsActive);
        Assert.NotEqual(originalSecurityStamp, await userManager.GetSecurityStampAsync(user));
        Assert.True(await userManager.IsInRoleAsync(user, "Administrador"));
        Assert.False(await userManager.IsInRoleAsync(user, "Cliente"));
    }
}
