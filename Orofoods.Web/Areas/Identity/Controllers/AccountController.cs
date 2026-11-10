using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Identity.Controllers;

[Area("Identity"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AdminUserService userService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> ChangeTemporaryPassword()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!user.MustChangePassword) return RedirectToAction("Index", "Home", new { area = "" });
        return View(new ChangeTemporaryPasswordViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeTemporaryPassword(ChangeTemporaryPasswordViewModel input)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        if (!ModelState.IsValid) return View(input);

        var result = await userService.ChangeTemporaryPasswordAsync(
            user.Id, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(input);
        }

        var updatedUser = await userManager.FindByIdAsync(user.Id);
        if (updatedUser is not null) await signInManager.RefreshSignInAsync(updatedUser);
        return RedirectToAction("Index", "Home", new { area = "" });
    }
}
