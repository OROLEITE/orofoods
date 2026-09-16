using Microsoft.AspNetCore.Identity;

namespace Orofoods.Web.Services.Identity;

public class AdminUserService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    public async Task UpdateAsync(string userId, bool isActive, int? customerId, int? salesRepresentativeId, string role)
    {
        if (!await roleManager.RoleExistsAsync(role)) throw new InvalidOperationException("Perfil de acesso invalido.");
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Usuario nao encontrado.");
        var activeStateChanged = user.IsActive != isActive;
        user.IsActive = isActive;
        user.CustomerId = customerId;
        user.SalesRepresentativeId = salesRepresentativeId;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) throw new InvalidOperationException(string.Join("; ", update.Errors.Select(x => x.Description)));
        if (activeStateChanged)
        {
            var stampUpdate = await userManager.UpdateSecurityStampAsync(user);
            if (!stampUpdate.Succeeded) throw new InvalidOperationException(string.Join("; ", stampUpdate.Errors.Select(x => x.Description)));
        }
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any()) await userManager.RemoveFromRolesAsync(user, roles);
        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded) throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
    }
}
