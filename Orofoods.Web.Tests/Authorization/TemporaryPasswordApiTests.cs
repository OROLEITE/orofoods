using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Controllers.Api.V1;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Authorization;

public sealed class TemporaryPasswordApiTests
{
    [Fact]
    public async Task Temporary_password_cannot_be_used_to_issue_an_api_token()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var user = new ApplicationUser
        {
            FullName = "Cliente temporário",
            UserName = "temporary-customer",
            Email = "temporary-customer@test.local",
            IsActive = true,
            MustChangePassword = true
        };
        Assert.True((await users.CreateAsync(user, "Temporary!Pass123")).Succeeded);
        var controller = new AuthController(users, null!, null!, null!);

        var result = await controller.Token(new TokenRequest(user.Email!, "Temporary!Pass123"));

        Assert.IsType<UnauthorizedResult>(result.Result);
    }
}
