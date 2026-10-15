using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Authorization;

public sealed class ApiTokenSecurityStampValidatorTests
{
    [Fact]
    public async Task Token_issued_before_temporary_reset_stays_invalid_after_password_change()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var roles = TestIdentityFactory.CreateRoleManager(db);
        var user = new ApplicationUser
        {
            FullName = "Cliente",
            UserName = "customer-login",
            Email = "customer@test.local",
            IsActive = true
        };
        Assert.True((await users.CreateAsync(user, "Original!Pass123")).Succeeded);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough",
            ["Jwt:Issuer"] = "tests",
            ["Jwt:Audience"] = "tests"
        }).Build();
        var tokenService = new ApiTokenService(configuration, users);
        var oldToken = Assert.IsType<ApiTokenResult>(await tokenService.CreateAsync(user));
        var oldContext = CreateValidatedContext(oldToken.AccessToken);
        var validator = new ApiTokenSecurityStampValidator(users);

        await validator.ValidateAsync(oldContext);
        Assert.Null(oldContext.Result?.Failure);

        var actor = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "administrator")], "tests"))
        };
        var userService = new AdminUserService(db, users, roles, new TestHttpContextAccessor { HttpContext = actor });
        Assert.True((await userService.ResetPasswordAsync(user.Id, "Temporary!Pass123", true)).Succeeded);
        Assert.True((await userService.ChangeTemporaryPasswordAsync(
            user.Id, "Temporary!Pass123", "Permanent!Pass123")).Succeeded);

        oldContext = CreateValidatedContext(oldToken.AccessToken);
        await validator.ValidateAsync(oldContext);

        Assert.NotNull(oldContext.Result?.Failure);
        Assert.Contains("invalidated", oldContext.Result!.Failure!.Message, StringComparison.OrdinalIgnoreCase);

        var reloadedUser = await users.FindByIdAsync(user.Id);
        Assert.NotNull(reloadedUser);
        var refreshed = Assert.IsType<ApiTokenResult>(await tokenService.CreateAsync(reloadedUser));
        var refreshedContext = CreateValidatedContext(refreshed.AccessToken);
        await validator.ValidateAsync(refreshedContext);
        Assert.Null(refreshedContext.Result?.Failure);
    }

    private static TokenValidatedContext CreateValidatedContext(string token)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "Bearer"));
        return new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("Bearer", "Bearer", typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = principal
        };
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
