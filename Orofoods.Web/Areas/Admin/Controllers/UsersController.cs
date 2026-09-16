using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public class UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AdminUserService service) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = await db.Users.AsNoTracking().Include(x => x.Customer).Include(x => x.SalesRepresentative).OrderBy(x => x.Email).ToListAsync();
        var rows = new List<AdminUserRow>();
        foreach (var user in users) rows.Add(new AdminUserRow(user, (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Sem perfil", user.Customer?.TradeName, user.SalesRepresentative?.Name));
        return View(rows);
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(await BuildAsync(user, (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Cliente"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserEditViewModel input)
    {
        try { await service.UpdateAsync(input.Id, input.IsActive, input.CustomerId, input.SalesRepresentativeId, input.Role); }
        catch (InvalidOperationException error) { ModelState.AddModelError(string.Empty, error.Message); var user = await userManager.FindByIdAsync(input.Id); if (user is null) return NotFound(); return View(await BuildAsync(user, input.Role)); }
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminUserEditViewModel> BuildAsync(ApplicationUser user, string role) => new()
    {
        Id = user.Id, Email = user.Email ?? "", IsActive = user.IsActive, CustomerId = user.CustomerId, SalesRepresentativeId = user.SalesRepresentativeId, Role = role,
        Customers = await db.Customers.OrderBy(x => x.TradeName).Select(x => new SelectListItem(x.TradeName, x.Id.ToString())).ToListAsync(),
        SalesRepresentatives = await db.SalesRepresentatives.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
        Roles = await roleManager.Roles.OrderBy(x => x.Name).Select(x => new SelectListItem(x.Name!, x.Name!)).ToListAsync()
    };
}
