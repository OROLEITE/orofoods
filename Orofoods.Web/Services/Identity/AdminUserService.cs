using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Services.Identity;

public sealed class AdminUserService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    private const string AdministratorRole = ApplicationRoles.Administrator;

    public async Task<IdentityResult> CreateAsync(CreateAdminUserInput input)
    {
        var name = input.Name?.Trim() ?? "";
        var email = input.Email?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) return Failure("NameRequired", "Informe o nome do usuário.");
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return Failure("InvalidEmail", "Informe um e-mail válido.");
        if (string.IsNullOrWhiteSpace(input.Role)) return Failure("RoleNotFound", "Perfil de acesso inválido.");
        if (string.IsNullOrWhiteSpace(input.Password)) return Failure("PasswordRequired", "Informe a senha inicial.");
        if (!await roleManager.RoleExistsAsync(input.Role)) return Failure("RoleNotFound", "Perfil de acesso inválido.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var duplicate = await userManager.FindByEmailAsync(email);
        if (duplicate is not null) return Failure("DuplicateEmail", "Já existe um usuário com este e-mail.");
        var links = await ValidateLinksAsync(input.CustomerId, input.SalesRepresentativeId);
        if (!links.Succeeded) return links;

        var user = new ApplicationUser
        {
            UserName = name,
            Email = email,
            IsActive = input.IsActive,
            CustomerId = input.CustomerId,
            SalesRepresentativeId = input.SalesRepresentativeId
        };
        var create = await userManager.CreateAsync(user, input.Password);
        if (!create.Succeeded)
        {
            await transaction.RollbackAsync();
            return create;
        }

        var addRole = await userManager.AddToRoleAsync(user, input.Role);
        if (!addRole.Succeeded)
        {
            await transaction.RollbackAsync();
            return addRole;
        }

        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(UpdateAdminUserInput input)
    {
        var user = await userManager.FindByIdAsync(input.Id);
        if (user is null) return Failure("UserNotFound", "Usuário não encontrado.");
        if (!string.Equals(user.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
            return Failure("ConcurrencyFailure", "Este usuário foi alterado por outra pessoa. Atualize a página e tente novamente.");

        var name = input.Name?.Trim() ?? "";
        var email = input.Email?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) return Failure("NameRequired", "Informe o nome do usuário.");
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return Failure("InvalidEmail", "Informe um e-mail válido.");
        if (string.IsNullOrWhiteSpace(input.Role)) return Failure("RoleNotFound", "Perfil de acesso inválido.");
        if (!await roleManager.RoleExistsAsync(input.Role)) return Failure("RoleNotFound", "Perfil de acesso inválido.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var duplicate = await userManager.FindByEmailAsync(email);
        if (duplicate is not null && duplicate.Id != user.Id)
            return Failure("DuplicateEmail", "Já existe um usuário com este e-mail.");
        var links = await ValidateLinksAsync(input.CustomerId, input.SalesRepresentativeId);
        if (!links.Succeeded) return links;

        var currentRoles = await userManager.GetRolesAsync(user);
        var removingLastAdministrator = user.IsActive && currentRoles.Contains(AdministratorRole, StringComparer.Ordinal)
            && (!input.IsActive || !string.Equals(input.Role, AdministratorRole, StringComparison.Ordinal));
        if (removingLastAdministrator && await CountActiveAdministratorsAsync() <= 1)
            return LastAdministratorFailure();

        if (!string.Equals(user.UserName, name, StringComparison.Ordinal))
        {
            var setName = await userManager.SetUserNameAsync(user, name);
            if (!setName.Succeeded) return await RollbackAsync(transaction, setName);
        }
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await userManager.SetEmailAsync(user, email);
            if (!setEmail.Succeeded) return await RollbackAsync(transaction, setEmail);
        }

        user.IsActive = input.IsActive;
        user.CustomerId = input.CustomerId;
        user.SalesRepresentativeId = input.SalesRepresentativeId;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return await RollbackAsync(transaction, update);

        if (!currentRoles.SequenceEqual([input.Role], StringComparer.Ordinal))
        {
            if (currentRoles.Count > 0)
            {
                var removeRoles = await userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeRoles.Succeeded) return await RollbackAsync(transaction, removeRoles);
            }

            var addRole = await userManager.AddToRoleAsync(user, input.Role);
            if (!addRole.Succeeded) return await RollbackAsync(transaction, addRole);
        }

        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return await RollbackAsync(transaction, stamp);
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetActiveAsync(string userId, bool isActive, string concurrencyStamp)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Failure("UserNotFound", "Usuário não encontrado.");
        if (!string.Equals(user.ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
            return Failure("ConcurrencyFailure", "Este usuário foi alterado por outra pessoa. Atualize a página e tente novamente.");
        if (user.IsActive == isActive) return IdentityResult.Success;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!isActive && user.IsActive && await userManager.IsInRoleAsync(user, AdministratorRole)
            && await CountActiveAdministratorsAsync() <= 1)
            return LastAdministratorFailure();

        user.IsActive = isActive;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return await RollbackAsync(transaction, update);
        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return await RollbackAsync(transaction, stamp);
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) return Failure("PasswordRequired", "Informe a nova senha.");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Failure("UserNotFound", "Usuário não encontrado.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return await userManager.ResetPasswordAsync(user, token, newPassword);
    }

    private async Task<IdentityResult> ValidateLinksAsync(int? customerId, int? salesRepresentativeId)
    {
        if (customerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == customerId.Value))
            return Failure("InvalidLink", "O cliente selecionado não existe.");
        if (salesRepresentativeId.HasValue && !await db.SalesRepresentatives.AnyAsync(x => x.Id == salesRepresentativeId.Value))
            return Failure("InvalidLink", "O vendedor selecionado não existe.");
        return IdentityResult.Success;
    }

    private async Task<int> CountActiveAdministratorsAsync()
    {
        var administrators = await userManager.GetUsersInRoleAsync(AdministratorRole);
        return administrators.Count(x => x.IsActive);
    }

    private static IdentityResult LastAdministratorFailure() =>
        Failure("LastAdministrator", "Não é possível remover o último Administrador ativo.");

    private static IdentityResult Failure(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });

    private static async Task<IdentityResult> RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, IdentityResult result)
    {
        await transaction.RollbackAsync();
        return result;
    }
}

public sealed record CreateAdminUserInput(
    string Name,
    string Email,
    string Password,
    string Role,
    bool IsActive,
    int? CustomerId,
    int? SalesRepresentativeId);

public sealed record UpdateAdminUserInput(
    string Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    int? CustomerId,
    int? SalesRepresentativeId,
    string ConcurrencyStamp);
