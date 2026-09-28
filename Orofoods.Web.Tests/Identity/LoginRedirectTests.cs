using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Identity.Pages.Account;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Identity;

public class LoginRedirectTests
{
    [Fact]
    public async Task Admin_without_return_url_redirects_to_admin_dashboard()
    {
        var result = await LoginAsAsync("Administrador", returnUrl: null);

        Assert.Equal("/Admin/Dashboard", RedirectUrl(result));
    }

    [Fact]
    public async Task Customer_without_return_url_redirects_to_customer_portal()
    {
        var result = await LoginAsAsync("Cliente", returnUrl: null);

        Assert.Equal("/Portal/Dashboard", RedirectUrl(result));
    }

    [Fact]
    public async Task Admin_with_valid_admin_return_url_is_returned_to_that_url()
    {
        var result = await LoginAsAsync("Administrador", "/Admin/Orders/Details/41");

        Assert.Equal("/Admin/Orders/Details/41", RedirectUrl(result));
    }

    [Fact]
    public async Task Admin_portal_return_url_does_not_open_customer_selection_automatically()
    {
        var result = await LoginAsAsync("Administrador", "/Portal/Dashboard");

        Assert.Equal("/Admin/Dashboard", RedirectUrl(result));
    }

    [Fact]
    public async Task Explicit_admin_customer_selection_return_url_is_preserved()
    {
        var result = await LoginAsAsync("Administrador", "/Portal/SelectCustomer");

        Assert.Equal("/Portal/SelectCustomer", RedirectUrl(result));
    }

    [Theory]
    [InlineData("Vendedor")]
    [InlineData("GerenteComercial")]
    public async Task Internal_roles_keep_the_existing_home_default_without_return_url(string role)
    {
        var result = await LoginAsAsync(role, returnUrl: null);

        Assert.Equal("/", RedirectUrl(result));
    }

    [Theory]
    [InlineData("Vendedor", "/Vendedor/Dashboard")]
    [InlineData("GerenteComercial", "/Admin/Commercial")]
    public async Task Internal_roles_keep_valid_return_urls(string role, string returnUrl)
    {
        var result = await LoginAsAsync(role, returnUrl);

        Assert.Equal(returnUrl, RedirectUrl(result));
    }

    [Fact]
    public void Anonymous_users_are_allowed_to_open_login_while_portal_requires_authorization()
    {
        Assert.Contains(typeof(AllowAnonymousAttribute), typeof(LoginModel).GetCustomAttributes(inherit: true).Select(attribute => attribute.GetType()));
        Assert.Contains(typeof(AuthorizeAttribute), typeof(PortalController).GetCustomAttributes(inherit: true).Select(attribute => attribute.GetType()));
    }

    private static async Task<IActionResult> LoginAsAsync(string role, string? returnUrl)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userManager = TestIdentityFactory.CreateUserManager(db);
        var roleManager = TestIdentityFactory.CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole(role));

        var email = $"{role.ToLowerInvariant()}@orofoods.test";
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true };
        await userManager.CreateAsync(user, "SenhaSegura123!");
        await userManager.AddToRoleAsync(user, role);

        var signInManager = CreateSignInManager(userManager);
        var model = new LoginModel(signInManager.Object, userManager, NullLogger<LoginModel>.Instance)
        {
            Url = CreateUrlHelper(),
            Input = new LoginModel.InputModel { Email = email, Password = "SenhaSegura123!" }
        };

        return await model.OnPostAsync(returnUrl);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(UserManager<ApplicationUser> userManager)
    {
        var contextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var claimsFactory = Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var schemeProvider = Mock.Of<IAuthenticationSchemeProvider>();
        var confirmation = Mock.Of<IUserConfirmation<ApplicationUser>>();
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager,
            contextAccessor,
            claimsFactory,
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            schemeProvider,
            confirmation);

        signInManager.Setup(manager => manager.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(Array.Empty<AuthenticationScheme>());
        signInManager.Setup(manager => manager.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        return signInManager;
    }

    private static IUrlHelper CreateUrlHelper()
    {
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(helper => helper.Content("~/")).Returns("/");
        urlHelper.Setup(helper => helper.IsLocalUrl(It.IsAny<string?>()))
            .Returns<string?>(url => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//"));
        urlHelper.Setup(helper => helper.Action(It.IsAny<UrlActionContext>()))
            .Returns<UrlActionContext>(context => context.Controller switch
            {
                "Dashboard" => "/Admin/Dashboard",
                "Portal" => "/Portal/Dashboard",
                _ => "/"
            });
        return urlHelper.Object;
    }

    private static string? RedirectUrl(IActionResult result) => result switch
    {
        LocalRedirectResult redirect => redirect.Url,
        RedirectResult redirect => redirect.Url,
        _ => null
    };
}
