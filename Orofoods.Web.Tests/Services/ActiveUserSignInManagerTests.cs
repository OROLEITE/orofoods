using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class ActiveUserSignInManagerTests
{
    [Fact]
    public async Task Inactive_user_cannot_sign_in()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userManager = TestIdentityFactory.CreateUserManager(db);
        var user = new ApplicationUser
        {
            UserName = "inactive@orofoods.local",
            Email = "inactive@orofoods.local",
            IsActive = false
        };
        await userManager.CreateAsync(user);

        var sut = CreateSignInManager(userManager);

        Assert.False(await sut.CanSignInAsync(user));
    }

    [Fact]
    public async Task Active_user_can_sign_in()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userManager = TestIdentityFactory.CreateUserManager(db);
        var user = new ApplicationUser
        {
            UserName = "active@orofoods.local",
            Email = "active@orofoods.local",
            IsActive = true
        };
        await userManager.CreateAsync(user);

        var sut = CreateSignInManager(userManager);

        Assert.True(await sut.CanSignInAsync(user));
    }

    private static ActiveUserSignInManager CreateSignInManager(UserManager<ApplicationUser> userManager)
    {
        var identityOptions = Options.Create(new IdentityOptions());
        return new ActiveUserSignInManager(
            userManager,
            new HttpContextAccessor(),
            new UserClaimsPrincipalFactory<ApplicationUser>(userManager, identityOptions),
            identityOptions,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<ApplicationUser>());
    }
}
