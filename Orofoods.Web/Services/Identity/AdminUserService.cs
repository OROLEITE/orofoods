using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Services.Identity;

public sealed class AdminUserService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IHttpContextAccessor? httpContextAccessor = null)
{
    private const string AdministratorRole = ApplicationRoles.Administrator;

    public async Task<IdentityResult> CreateAsync(CreateAdminUserInput input)
    {
        if (!TryGetActor(out var actorId, out var actorName)) return ActorRequiredFailure();
        var fullName = input.Name?.Trim() ?? "";
        var userName = string.IsNullOrWhiteSpace(input.UserName) ? fullName : input.UserName.Trim();
        var email = input.Email?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fullName)) return Failure("NameRequired", "Informe o nome do usuário.");
        if (fullName.Length > 256) return Failure("NameTooLong", "O nome deve ter até 256 caracteres.");
        if (string.IsNullOrWhiteSpace(userName)) return Failure("UserNameRequired", "Informe o login.");
        if (userName.Length > 256) return Failure("UserNameTooLong", "O login deve ter até 256 caracteres.");
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return Failure("InvalidEmail", "Informe um e-mail válido.");
        if (string.IsNullOrWhiteSpace(input.Role)) return Failure("RoleNotFound", "Perfil de acesso inválido.");
        if (string.IsNullOrWhiteSpace(input.Password)) return Failure("PasswordRequired", "Informe a senha inicial.");
        if (!await roleManager.RoleExistsAsync(input.Role)) return Failure("RoleNotFound", "Perfil de acesso inválido.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await userManager.FindByEmailAsync(email) is not null)
            return Failure("DuplicateEmail", "Já existe um usuário com este e-mail.");
        var links = await ValidateLinksAsync(input.CustomerId, input.SalesRepresentativeId);
        if (!links.Succeeded) return links;

        var user = new ApplicationUser
        {
            FullName = fullName,
            UserName = userName,
            Email = email,
            IsActive = input.IsActive,
            MustChangePassword = input.MustChangePassword,
            CustomerId = input.CustomerId,
            SalesRepresentativeId = input.SalesRepresentativeId
        };
        var create = await userManager.CreateAsync(user, input.Password);
        if (!create.Succeeded) return await RollbackAsync(transaction, create);

        var addRole = await userManager.AddToRoleAsync(user, input.Role);
        if (!addRole.Succeeded) return await RollbackAsync(transaction, addRole);

        AddAudit(actorId, actorName, user, "Created", "Conta, perfil, status e vínculos cadastrados.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(UpdateAdminUserInput input)
    {
        if (!TryGetActor(out var actorId, out var actorName)) return ActorRequiredFailure();
        var user = await userManager.FindByIdAsync(input.Id);
        if (user is null) return Failure("UserNotFound", "Usuário não encontrado.");
        if (!string.Equals(user.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
            return Failure("ConcurrencyFailure", "Este usuário foi alterado por outra pessoa. Atualize a página e tente novamente.");

        var fullName = input.Name?.Trim() ?? "";
        var userName = string.IsNullOrWhiteSpace(input.UserName) ? fullName : input.UserName.Trim();
        var email = input.Email?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fullName)) return Failure("NameRequired", "Informe o nome do usuário.");
        if (fullName.Length > 256) return Failure("NameTooLong", "O nome deve ter até 256 caracteres.");
        if (string.IsNullOrWhiteSpace(userName)) return Failure("UserNameRequired", "Informe o login.");
        if (userName.Length > 256) return Failure("UserNameTooLong", "O login deve ter até 256 caracteres.");
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return Failure("InvalidEmail", "Informe um e-mail válido.");
        if (string.IsNullOrWhiteSpace(input.Role) || !await roleManager.RoleExistsAsync(input.Role))
            return Failure("RoleNotFound", "Perfil de acesso inválido.");

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

        var changedFields = new List<string>();
        if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal)) changedFields.Add("nome");
        if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
        {
            var setName = await userManager.SetUserNameAsync(user, userName);
            if (!setName.Succeeded) return await RollbackAsync(transaction, setName);
            changedFields.Add("login");
        }
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await userManager.SetEmailAsync(user, email);
            if (!setEmail.Succeeded) return await RollbackAsync(transaction, setEmail);
            changedFields.Add("e-mail");
        }
        if (user.IsActive != input.IsActive) changedFields.Add("status");
        if (user.CustomerId != input.CustomerId) changedFields.Add("cliente vinculado");
        if (user.SalesRepresentativeId != input.SalesRepresentativeId) changedFields.Add("vendedor vinculado");
        if (!currentRoles.SequenceEqual([input.Role], StringComparer.Ordinal)) changedFields.Add("perfil");
        if (changedFields.Count == 0) return IdentityResult.Success;

        user.FullName = fullName;
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
        if (changedFields.Count > 0)
        {
            AddAudit(actorId, actorName, user, "Updated", $"Campos atualizados: {string.Join(", ", changedFields)}.");
            await db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetActiveAsync(string userId, bool isActive, string concurrencyStamp)
    {
        if (!TryGetActor(out var actorId, out var actorName)) return ActorRequiredFailure();
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
        AddAudit(actorId, actorName, user, "StatusChanged", isActive ? "Conta ativada." : "Conta desativada.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, bool mustChangePassword = false)
    {
        if (!TryGetActor(out var actorId, out var actorName)) return ActorRequiredFailure();
        if (string.IsNullOrWhiteSpace(newPassword)) return Failure("PasswordRequired", "Informe a nova senha.");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Failure("UserNotFound", "Usuário não encontrado.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!reset.Succeeded) return await RollbackAsync(transaction, reset);
        user.MustChangePassword = mustChangePassword;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return await RollbackAsync(transaction, update);
        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return await RollbackAsync(transaction, stamp);
        AddAudit(actorId, actorName, user, "PasswordReset",
            mustChangePassword ? "Senha redefinida; troca obrigatória no próximo acesso." : "Senha redefinida.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ChangeTemporaryPasswordAsync(string userId, string currentPassword, string newPassword)
    {
        if (!TryGetActor(out var actorId, out var actorName)) return ActorRequiredFailure();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Failure("UserNotFound", "Usuário não encontrado.");
        if (!user.MustChangePassword) return Failure("TemporaryPasswordNotRequired", "Esta conta não exige troca de senha.");
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return Failure("PasswordRequired", "Informe a senha atual e a nova senha.");
        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            return Failure("NewPasswordMustDiffer", "A nova senha deve ser diferente da senha temporária.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var change = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!change.Succeeded) return await RollbackAsync(transaction, change);
        user.MustChangePassword = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return await RollbackAsync(transaction, update);
        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return await RollbackAsync(transaction, stamp);
        AddAudit(actorId, actorName, user, "PasswordChanged", "Senha temporária trocada pelo titular.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return IdentityResult.Success;
    }

    public async Task<IReadOnlyList<AdminUserChangeAudit>> GetHistoryAsync(string targetUserId, int take = 100) =>
        await db.AdminUserChangeAudits.AsNoTracking()
            .Where(entry => entry.TargetUserId == targetUserId)
            .OrderByDescending(entry => entry.ChangedAt)
            .ThenByDescending(entry => entry.Id)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync();

    private bool TryGetActor(out string actorId, out string actorName)
    {
        var principal = httpContextAccessor?.HttpContext?.User;
        actorId = principal is null ? "" : userManager.GetUserId(principal) ?? "";
        actorName = principal?.Identity?.Name ?? actorId;
        return !string.IsNullOrWhiteSpace(actorId);
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

    private void AddAudit(string actorId, string actorName, ApplicationUser target, string action, string summary) =>
        db.AdminUserChangeAudits.Add(new AdminUserChangeAudit
        {
            ActorUserId = actorId,
            ActorName = actorName,
            TargetUserId = target.Id,
            TargetName = target.FullName,
            Action = action,
            Summary = summary,
            ChangedAt = DateTime.UtcNow
        });

    private static IdentityResult ActorRequiredFailure() =>
        Failure("ActorRequired", "Não foi possível identificar o Administrador responsável.");

    private static IdentityResult LastAdministratorFailure() =>
        Failure("LastAdministrator", "Não é possível remover o último Administrador ativo.");

    private static IdentityResult Failure(string code, string description) =>
        IdentityResult.Failed(new IdentityError { Code = code, Description = description });

    private static async Task<IdentityResult> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        IdentityResult result)
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
    int? SalesRepresentativeId,
    string? UserName = null,
    bool MustChangePassword = false);

public sealed record UpdateAdminUserInput(
    string Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    int? CustomerId,
    int? SalesRepresentativeId,
    string ConcurrencyStamp,
    string? UserName = null);
