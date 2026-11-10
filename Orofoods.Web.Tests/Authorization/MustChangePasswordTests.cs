using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Orofoods.Web.Infrastructure;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Authorization;

public sealed class MustChangePasswordTests
{
    [Fact]
    public async Task Temporary_password_account_is_redirected_before_protected_routes_run()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var user = await CreateTemporaryUserAsync(users);
        var context = CreateContext(user.Id, "/Admin/Users");
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, users);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/Identity/Account/ChangeTemporaryPassword", context.Response.Headers.Location);
    }

    [Fact]
    public async Task Temporary_password_account_can_reach_change_and_logout_only()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var user = await CreateTemporaryUserAsync(users);

        foreach (var path in new[] { "/Identity/Account/ChangeTemporaryPassword", "/Identity/Account/Logout" })
        {
            var context = CreateContext(user.Id, path);
            var nextCalled = false;
            var middleware = new MustChangePasswordMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, users);

            Assert.True(nextCalled, path);
        }
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/lib/bootstrap/dist/css/bootstrap.min.css")]
    [InlineData("/js/site.js")]
    [InlineData("/images/logo.png")]
    [InlineData("/favicon.ico")]
    public async Task Temporary_password_account_can_load_static_assets(string path)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var user = await CreateTemporaryUserAsync(users);
        var context = CreateContext(user.Id, path);
        context.Request.Method = HttpMethods.Get;
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, users);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    [Fact]
    public async Task Temporary_password_account_cannot_post_to_static_paths_or_lookalike_routes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var user = await CreateTemporaryUserAsync(users);

        foreach (var (method, path) in new[] { ("POST", "/css/site.css"), ("GET", "/cssx/secret"), ("GET", "/Portal"), ("GET", "/appsettings.json"), ("GET", "/web.config"), ("GET", "/DataProtectionKeys/key.xml"), ("GET", "/Admin/Users"), ("GET", "/api/orders") })
        {
            var context = CreateContext(user.Id, path);
            context.Request.Method = method;
            var nextCalled = false;
            var middleware = new MustChangePasswordMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, users);

            Assert.False(nextCalled, path);
            Assert.Equal(path.StartsWith("/api") ? StatusCodes.Status403Forbidden : StatusCodes.Status302Found, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task Temporary_password_api_call_returns_forbidden_and_regular_user_continues()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var temporary = await CreateTemporaryUserAsync(users);
        var apiContext = CreateContext(temporary.Id, "/api/orders");
        var apiMiddleware = new MustChangePasswordMiddleware(_ => Task.CompletedTask);

        await apiMiddleware.InvokeAsync(apiContext, users);

        Assert.Equal(StatusCodes.Status403Forbidden, apiContext.Response.StatusCode);

        temporary.MustChangePassword = false;
        await users.UpdateAsync(temporary);
        var regularContext = CreateContext(temporary.Id, "/Admin/Users");
        var nextCalled = false;
        var regularMiddleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await regularMiddleware.InvokeAsync(regularContext, users);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Temporary_password_bearer_identity_is_loaded_from_endpoint_authentication_metadata()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var users = TestIdentityFactory.CreateUserManager(db);
        var user = await CreateTemporaryUserAsync(users);
        var context = CreateContext(user.Id, "/api/v1/orders");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id)], "Bearer"));
        var authentication = new Mock<IAuthenticationService>();
        authentication.Setup(service => service.AuthenticateAsync(context, "Bearer"))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, "Bearer")));
        context.RequestServices = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute { AuthenticationSchemes = "Bearer" }),
            "protected-api"));
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, users);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        authentication.Verify(service => service.AuthenticateAsync(context, "Bearer"), Times.Once);
    }

    private static async Task<ApplicationUser> CreateTemporaryUserAsync(Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users)
    {
        var user = new ApplicationUser
        {
            FullName = "Conta temporária",
            UserName = $"temporary-{Guid.NewGuid():N}",
            Email = $"temporary-{Guid.NewGuid():N}@test.local",
            MustChangePassword = true
        };
        var created = await users.CreateAsync(user, "Temporary!Pass123");
        Assert.True(created.Succeeded);
        return user;
    }

    private static DefaultHttpContext CreateContext(string userId, string path) => new()
    {
        Request = { Path = path },
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], "cookie"))
    };
}
