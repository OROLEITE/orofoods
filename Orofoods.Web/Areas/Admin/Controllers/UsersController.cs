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

[Area("Admin"), Authorize(Roles = ApplicationRoles.Administrator)]
public class UsersController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    AdminUserService service) : Controller
{
    private const int MaxRows = 200;

    [HttpGet]
    public async Task<IActionResult> Index(string? query, string? role, bool? isActive)
    {
        var usersQuery = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            usersQuery = usersQuery.Where(user => user.UserName!.Contains(search) || user.Email!.Contains(search));
        }
        if (isActive.HasValue) usersQuery = usersQuery.Where(user => user.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(role))
        {
            usersQuery = from user in usersQuery
                         join userRole in db.UserRoles on user.Id equals userRole.UserId
                         join identityRole in db.Roles on userRole.RoleId equals identityRole.Id
                         where identityRole.Name == role
                         select user;
        }

        var users = await usersQuery
            .Include(user => user.Customer)
            .Include(user => user.SalesRepresentative)
            .OrderBy(user => user.UserName)
            .Take(MaxRows)
            .ToListAsync();
        var userIds = users.Select(user => user.Id).ToArray();
        var roleAssignments = await (from userRole in db.UserRoles.AsNoTracking()
                                     join identityRole in db.Roles.AsNoTracking() on userRole.RoleId equals identityRole.Id
                                     where userIds.Contains(userRole.UserId)
                                     select new { userRole.UserId, identityRole.Name })
            .ToListAsync();
        var rolesByUser = roleAssignments.GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Sem perfil");
        var rows = users.Select(user => new AdminUserRow(
            user,
            rolesByUser.GetValueOrDefault(user.Id, "Sem perfil"),
            user.Customer?.TradeName,
            user.SalesRepresentative?.Name)).ToList();

        return View(new AdminUserListViewModel
        {
            Users = rows,
            Query = query,
            Role = role,
            IsActive = isActive,
            Roles = await BuildRolesAsync(role)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new AdminUserCreateViewModel();
        await PopulateChoicesAsync(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserCreateViewModel input)
    {
        if (!ModelState.IsValid)
        {
            await PopulateChoicesAsync(input);
            return View(input);
        }

        var result = await service.CreateAsync(new CreateAdminUserInput(
            input.Name, input.Email, input.Password, input.Role, input.IsActive,
            input.CustomerId, input.SalesRepresentativeId));
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            await PopulateChoicesAsync(input);
            return View(input);
        }

        TempData["SuccessMessage"] = "Usuário criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var model = await BuildEditAsync(user, (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Cliente");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserEditViewModel input)
    {
        if (!ModelState.IsValid)
        {
            await PopulateChoicesAsync(input);
            return View(input);
        }

        var result = await service.UpdateAsync(new UpdateAdminUserInput(
            input.Id, input.Name, input.Email, input.Role, input.IsActive,
            input.CustomerId, input.SalesRepresentativeId, input.ConcurrencyStamp));
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            var currentUser = await userManager.FindByIdAsync(input.Id);
            if (currentUser is null) return NotFound();
            if (result.Errors.Any(error => error.Code == "ConcurrencyFailure"))
            {
                ModelState.Clear();
                ModelState.AddModelError(string.Empty, "Este usuário foi alterado por outra pessoa. Os dados atuais foram carregados; revise e tente novamente.");
                input = await BuildEditAsync(currentUser, (await userManager.GetRolesAsync(currentUser)).FirstOrDefault() ?? "Cliente");
            }
            else
            {
                await PopulateChoicesAsync(input);
            }
            return View(input);
        }

        TempData["SuccessMessage"] = "Usuário atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id, string concurrencyStamp)
    {
        var result = await service.SetActiveAsync(id, true, concurrencyStamp);
        return await FinishStatusChangeAsync(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id, string concurrencyStamp)
    {
        var result = await service.SetActiveAsync(id, false, concurrencyStamp);
        return await FinishStatusChangeAsync(result);
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        return View(new AdminUserResetPasswordViewModel { Id = user.Id, Name = user.UserName ?? "", Email = user.Email ?? "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AdminUserResetPasswordViewModel input)
    {
        var user = await userManager.FindByIdAsync(input.Id);
        if (user is null) return NotFound();
        if (!input.ConfirmReset) ModelState.AddModelError(nameof(input.ConfirmReset), "Confirme que deseja redefinir a senha deste usuário.");
        if (!ModelState.IsValid)
        {
            input.Name = user.UserName ?? "";
            input.Email = user.Email ?? "";
            return View(input);
        }

        var result = await service.ResetPasswordAsync(user.Id, input.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            input.Name = user.UserName ?? "";
            input.Email = user.Email ?? "";
            return View(input);
        }

        TempData["SuccessMessage"] = "Senha redefinida com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> FinishStatusChangeAsync(IdentityResult result)
    {
        if (!result.Succeeded)
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(error => error.Description));
        else
            TempData["SuccessMessage"] = "Status do usuário atualizado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminUserEditViewModel> BuildEditAsync(ApplicationUser user, string role)
    {
        var model = new AdminUserEditViewModel
        {
            Id = user.Id,
            Name = user.UserName ?? "",
            Email = user.Email ?? "",
            ConcurrencyStamp = user.ConcurrencyStamp ?? "",
            IsActive = user.IsActive,
            CustomerId = user.CustomerId,
            SalesRepresentativeId = user.SalesRepresentativeId,
            Role = role
        };
        await PopulateChoicesAsync(model);
        return model;
    }

    private async Task PopulateChoicesAsync(AdminUserEditViewModel model)
    {
        model.Customers = await db.Customers.AsNoTracking().OrderBy(item => item.TradeName)
            .Select(item => new SelectListItem(item.TradeName, item.Id.ToString())).ToListAsync();
        model.SalesRepresentatives = await db.SalesRepresentatives.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new SelectListItem(item.Name, item.Id.ToString())).ToListAsync();
        model.Roles = await BuildRolesAsync(model.Role);
    }

    private async Task PopulateChoicesAsync(AdminUserCreateViewModel model)
    {
        model.Customers = await db.Customers.AsNoTracking().OrderBy(item => item.TradeName)
            .Select(item => new SelectListItem(item.TradeName, item.Id.ToString())).ToListAsync();
        model.SalesRepresentatives = await db.SalesRepresentatives.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new SelectListItem(item.Name, item.Id.ToString())).ToListAsync();
        model.Roles = await BuildRolesAsync(model.Role);
    }

    private async Task<List<SelectListItem>> BuildRolesAsync(string? selectedRole) => await roleManager.Roles
        .AsNoTracking().OrderBy(item => item.Name)
        .Select(item => new SelectListItem(item.Name!, item.Name!, item.Name == selectedRole))
        .ToListAsync();

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
    }
}
