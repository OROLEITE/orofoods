using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orofoods.Web.Areas.Identity.Pages.Account;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Tests.Infrastructure;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

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

    [Theory]
    [InlineData("staging-admin-test", "staging-admin-test@orofoods.test", "Administrador", "/Admin/Dashboard")]
    [InlineData("staging-customer-test", "staging-customer-test@orofoods.test", "Cliente", "/Portal/Dashboard")]
    public async Task Login_by_email_authenticates_accounts_whose_user_name_is_different(
        string userName,
        string email,
        string role,
        string expectedRedirect)
    {
        var attempt = await AttemptLoginAsync(userName, email, role);

        Assert.Equal(expectedRedirect, RedirectUrl(attempt.Result));
        attempt.SignInManager.Verify(manager => manager.PasswordSignInAsync(
            It.Is<ApplicationUser>(user => user.UserName == userName && user.Email == email),
            "SenhaSegura123!",
            false,
            false), Times.Once);
    }

    [Fact]
    public async Task Nonexistent_email_shows_a_generic_login_error()
    {
        var attempt = await AttemptLoginAsync("missing-user", "missing@orofoods.test", "Cliente", createUser: false);

        AssertGenericLoginError(attempt);
        attempt.SignInManager.Verify(manager => manager.PasswordSignInAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_return_the_same_public_error()
    {
        var unknownEmail = await AttemptLoginAsync("missing-user", "missing@orofoods.test", "Cliente", createUser: false);
        var wrongPassword = await AttemptLoginAsync("customer-login", "customer@orofoods.test", "Cliente", signInResult: IdentitySignInResult.Failed);

        Assert.Equal(PublicLoginError(unknownEmail), PublicLoginError(wrongPassword));
    }

    [Fact]
    public async Task Incorrect_password_shows_a_generic_login_error()
    {
        var attempt = await AttemptLoginAsync("customer-login", "customer@orofoods.test", "Cliente", signInResult: IdentitySignInResult.Failed);

        AssertGenericLoginError(attempt);
    }

    [Fact]
    public async Task Locked_out_user_keeps_the_existing_lockout_redirect()
    {
        var attempt = await AttemptLoginAsync("customer-login", "customer@orofoods.test", "Cliente", signInResult: IdentitySignInResult.LockedOut);

        Assert.Equal("./Lockout", Assert.IsType<RedirectToPageResult>(attempt.Result).PageName);
    }

    [Fact]
    public async Task Sign_in_not_allowed_shows_a_generic_login_error()
    {
        var attempt = await AttemptLoginAsync("customer-login", "customer@orofoods.test", "Cliente", signInResult: IdentitySignInResult.NotAllowed);

        AssertGenericLoginError(attempt);
    }

    [Fact]
    public async Task Remember_me_is_passed_to_the_identity_user_sign_in_overload()
    {
        var attempt = await AttemptLoginAsync("customer-login", "customer@orofoods.test", "Cliente", rememberMe: true);

        Assert.Equal("/Portal/Dashboard", RedirectUrl(attempt.Result));
        attempt.SignInManager.Verify(manager => manager.PasswordSignInAsync(
            It.Is<ApplicationUser>(user => user.UserName == "customer-login"),
            "SenhaSegura123!",
            true,
            false), Times.Once);
    }

    [Fact]
    public async Task Email_whitespace_is_trimmed_before_user_lookup()
    {
        var attempt = await AttemptLoginAsync("customer-login", "customer@orofoods.test", "Cliente", inputEmail: " customer@orofoods.test ");

        Assert.Equal("/Portal/Dashboard", RedirectUrl(attempt.Result));
    }

    [Fact]
    public async Task External_return_url_does_not_redirect_outside_the_portal()
    {
        var attempt = await AttemptLoginAsync("admin-login", "admin@orofoods.test", "Administrador", returnUrl: "https://evil.example/");

        Assert.Equal("/Admin/Dashboard", RedirectUrl(attempt.Result));
    }

    private static async Task<IActionResult> LoginAsAsync(string role, string? returnUrl)
    {
        var email = $"{role.ToLowerInvariant()}@orofoods.test";
        return (await AttemptLoginAsync($"login-{role.ToLowerInvariant()}", email, role, returnUrl)).Result;
    }

    private static async Task<LoginAttempt> AttemptLoginAsync(
        string userName,
        string email,
        string role,
        string? returnUrl = null,
        bool createUser = true,
        IdentitySignInResult? signInResult = null,
        bool rememberMe = false,
        string? inputEmail = null)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userManager = TestIdentityFactory.CreateUserManager(db);
        var roleManager = TestIdentityFactory.CreateRoleManager(db);
        await roleManager.CreateAsync(new IdentityRole(role));

        if (createUser)
        {
            var user = new ApplicationUser { UserName = userName, Email = email, IsActive = true, EmailConfirmed = true };
            await userManager.CreateAsync(user, "SenhaSegura123!");
            await userManager.AddToRoleAsync(user, role);
        }

        var signInManager = CreateSignInManager(userManager, signInResult ?? IdentitySignInResult.Success);
        var model = new LoginModel(signInManager.Object, userManager, NullLogger<LoginModel>.Instance)
        {
            Url = CreateUrlHelper(),
            Input = new LoginModel.InputModel { Email = inputEmail ?? email, Password = "SenhaSegura123!", RememberMe = rememberMe }
        };

        return new LoginAttempt(await model.OnPostAsync(returnUrl), model, signInManager);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(
        UserManager<ApplicationUser> userManager,
        IdentitySignInResult signInResult)
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
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(signInResult);
        signInManager.Setup(manager => manager.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentitySignInResult.Failed);

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

    private static void AssertGenericLoginError(LoginAttempt attempt)
    {
        Assert.IsType<PageResult>(attempt.Result);
        var error = PublicLoginError(attempt);
        Assert.Equal("Não foi possível acessar com estas credenciais.", error);
        Assert.DoesNotContain("SenhaSegura123!", error, StringComparison.Ordinal);
    }

    private static string PublicLoginError(LoginAttempt attempt) =>
        Assert.Single(attempt.Model.ModelState[string.Empty]!.Errors).ErrorMessage;

    private sealed record LoginAttempt(
        IActionResult Result,
        LoginModel Model,
        Mock<SignInManager<ApplicationUser>> SignInManager);
}
