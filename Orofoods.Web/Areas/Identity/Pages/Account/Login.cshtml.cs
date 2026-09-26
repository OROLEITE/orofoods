using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILogger<LoginModel> logger) : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILogger<LoginModel> _logger = logger;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<AuthenticationScheme> ExternalLogins { get; set; } = [];

    public string ReturnUrl { get; set; } = "/";

    [TempData]
    public string? ErrorMessage { get; set; }

    private static bool IsAdministratorReturnUrl(string returnUrl)
    {
        var path = GetReturnPath(returnUrl);
        var isCustomerPortalPath = IsPathOrSubpath(path, "/Portal");
        var isExplicitCustomerSelection = string.Equals(path, "/Portal/SelectCustomer", StringComparison.OrdinalIgnoreCase);

        return (!isCustomerPortalPath || isExplicitCustomerSelection)
            && !IsPathOrSubpath(path, "/Vendedor");
    }

    private static bool IsCustomerReturnUrl(string returnUrl)
    {
        var path = GetReturnPath(returnUrl);
        return !IsPathOrSubpath(path, "/Admin")
            && !IsPathOrSubpath(path, "/Vendedor")
            && !string.Equals(path, "/Portal/SelectCustomer", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetReturnPath(string returnUrl)
    {
        var queryOrFragment = returnUrl.IndexOfAny(['?', '#']);
        return queryOrFragment < 0 ? returnUrl : returnUrl[..queryOrFragment];
    }

    private static bool IsPathOrSubpath(string path, string basePath) =>
        string.Equals(path, basePath, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase);

    public class InputModel
    {
        [Required(ErrorMessage = "O e-mail corporativo \u00e9 obrigat\u00f3rio.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail corporativo v\u00e1lido.")]
        [Display(Name = "E-mail corporativo")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha \u00e9 obrigat\u00f3ria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Manter meu acesso")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl ?? Url.Content("~/");
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            var user = await _userManager.FindByEmailAsync(Input.Email);
            var isAdministrator = user is not null && await _userManager.IsInRoleAsync(user, "Administrador");
            var isCustomer = user is not null && await _userManager.IsInRoleAsync(user, "Cliente");
            var homeUrl = Url.Content("~/");
            var adminDashboardUrl = Url.Action("Index", "Dashboard", new { area = "Admin" }) ?? "/Admin/Dashboard";
            var customerDashboardUrl = Url.Action("Dashboard", "Portal", new { area = "" }) ?? "/Portal/Dashboard";
            var hasLocalReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
                && Url.IsLocalUrl(returnUrl)
                && !string.Equals(returnUrl, homeUrl, StringComparison.Ordinal);

            if (isAdministrator)
            {
                if (hasLocalReturnUrl && IsAdministratorReturnUrl(returnUrl!))
                {
                    return LocalRedirect(returnUrl!);
                }

                return LocalRedirect(adminDashboardUrl);
            }

            if (isCustomer)
            {
                if (hasLocalReturnUrl && IsCustomerReturnUrl(returnUrl!))
                {
                    return LocalRedirect(returnUrl!);
                }

                return LocalRedirect(customerDashboardUrl);
            }

            return LocalRedirect(hasLocalReturnUrl ? returnUrl! : homeUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "N\u00e3o foi poss\u00edvel acessar com estas credenciais.");
        return Page();
    }
}
