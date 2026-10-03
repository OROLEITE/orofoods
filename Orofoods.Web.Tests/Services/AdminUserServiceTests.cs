using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class AdminUserServiceTests
{
    [Fact]
    public async Task Create_uses_Identity_and_assigns_existing_role_and_links()
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = await fixture.AddCustomerAsync("Cliente teste");
        var seller = await fixture.AddSellerAsync("Vendedor teste");

        var result = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Rui Gonçalves", "operador@test.local", "Valid!Pass123", "Operador", true, customer.Id, seller.Id));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var created = Assert.Single(await fixture.UserManager.Users.Where(x => x.Email == "operador@test.local").ToListAsync());
        Assert.Equal("Rui Gonçalves", created.UserName);
        Assert.Equal("OPERADOR@TEST.LOCAL", created.NormalizedEmail);
        Assert.Equal(customer.Id, created.CustomerId);
        Assert.Equal(seller.Id, created.SalesRepresentativeId);
        Assert.True(await fixture.UserManager.IsInRoleAsync(created, "Operador"));
    }

    [Fact]
    public async Task Create_rejects_invalid_email()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Usuário", "email-invalido", "Valid!Pass123", "Operador", true, null, null));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Code == "InvalidEmail");
    }

    [Fact]
    public async Task Create_rejects_duplicate_email_without_creating_a_second_user()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreateUserAsync("Primeiro", "duplicado@test.local", "Operador");

        var result = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Segundo", "duplicado@test.local", "Valid!Pass123", "Operador", true, null, null));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Code == "DuplicateEmail");
        Assert.Equal(1, await fixture.UserManager.Users.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_duplicate_username()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreateUserAsync("Nome repetido", "first@test.local", "Operador");

        var result = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Nome repetido", "second@test.local", "Valid!Pass123", "Operador", true, null, null));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Code == "DuplicateUserName");
        Assert.Equal(1, await fixture.UserManager.Users.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_password_that_fails_Identity_policy()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Usuário", "user@test.local", "weak", "Operador", true, null, null));

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(0, await fixture.UserManager.Users.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_missing_role_and_invalid_links()
    {
        await using var fixture = await Fixture.CreateAsync();

        var missingRole = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Usuário", "user@test.local", "Valid!Pass123", "Inexistente", true, null, null));
        var missingLink = await fixture.Sut.CreateAsync(new CreateAdminUserInput(
            "Outro", "other@test.local", "Valid!Pass123", "Operador", true, 999, 999));

        Assert.False(missingRole.Succeeded);
        Assert.Contains(missingRole.Errors, x => x.Code == "RoleNotFound");
        Assert.False(missingLink.Succeeded);
        Assert.Contains(missingLink.Errors, x => x.Code == "InvalidLink");
        Assert.Equal(0, await fixture.UserManager.Users.CountAsync());
    }

    [Fact]
    public async Task Update_changes_identity_fields_role_and_optional_links()
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = await fixture.AddCustomerAsync("Cliente novo");
        var seller = await fixture.AddSellerAsync("Vendedor novo");
        var user = await fixture.CreateUserAsync("Nome antigo", "old@test.local", "Operador");
        var originalStamp = user.SecurityStamp;

        var result = await fixture.Sut.UpdateAsync(new UpdateAdminUserInput(
            user.Id, "Nome atualizado", "new@test.local", "Vendedor", true,
            customer.Id, seller.Id, user.ConcurrencyStamp!));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var updated = await fixture.UserManager.FindByIdAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal("Nome atualizado", updated.UserName);
        Assert.Equal("new@test.local", updated.Email);
        Assert.Equal("NEW@TEST.LOCAL", updated.NormalizedEmail);
        Assert.Equal(customer.Id, updated.CustomerId);
        Assert.Equal(seller.Id, updated.SalesRepresentativeId);
        Assert.NotEqual(originalStamp, updated.SecurityStamp);
        Assert.True(await fixture.UserManager.IsInRoleAsync(updated, "Vendedor"));
        Assert.False(await fixture.UserManager.IsInRoleAsync(updated, "Operador"));

        var unlinkResult = await fixture.Sut.UpdateAsync(new UpdateAdminUserInput(
            updated.Id, updated.UserName!, updated.Email!, "Vendedor", true, null, null, updated.ConcurrencyStamp!));
        Assert.True(unlinkResult.Succeeded);
        var unlinked = await fixture.UserManager.FindByIdAsync(user.Id);
        Assert.Null(unlinked!.CustomerId);
        Assert.Null(unlinked.SalesRepresentativeId);
    }

    [Fact]
    public async Task Update_rejects_stale_concurrency_stamp_without_overwriting()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Nome original", "user@test.local", "Operador");

        var result = await fixture.Sut.UpdateAsync(new UpdateAdminUserInput(
            user.Id, "Nome concorrente", "user@test.local", "Operador", true,
            null, null, "stale-stamp"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Code == "ConcurrencyFailure");
        Assert.Equal("Nome original", (await fixture.UserManager.FindByIdAsync(user.Id))!.UserName);
    }

    [Fact]
    public async Task Cannot_deactivate_the_last_active_administrator()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.CreateUserAsync("Admin", "admin@test.local", "Administrador");
        var stamp = admin.ConcurrencyStamp!;

        var result = await fixture.Sut.SetActiveAsync(admin.Id, false, stamp);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Code == "LastAdministrator");
        Assert.True((await fixture.UserManager.FindByIdAsync(admin.Id))!.IsActive);
    }

    [Fact]
    public async Task Cannot_remove_role_from_last_active_administrator()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.CreateUserAsync("Admin", "admin@test.local", "Administrador");

        var result = await fixture.Sut.UpdateAsync(new UpdateAdminUserInput(
            admin.Id, admin.UserName!, admin.Email!, "Cliente", true, null, null, admin.ConcurrencyStamp!));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, x => x.Code == "LastAdministrator");
        Assert.True(await fixture.UserManager.IsInRoleAsync(admin, "Administrador"));
    }

    [Fact]
    public async Task May_deactivate_an_administrator_when_another_active_administrator_remains()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CreateUserAsync("Admin um", "admin1@test.local", "Administrador");
        await fixture.CreateUserAsync("Admin dois", "admin2@test.local", "Administrador");

        var result = await fixture.Sut.SetActiveAsync(first.Id, false, first.ConcurrencyStamp!);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        Assert.False((await fixture.UserManager.FindByIdAsync(first.Id))!.IsActive);
    }

    [Fact]
    public async Task May_remove_administrator_role_when_another_active_administrator_remains()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CreateUserAsync("Admin um", "admin1@test.local", "Administrador");
        await fixture.CreateUserAsync("Admin dois", "admin2@test.local", "Administrador");

        var result = await fixture.Sut.UpdateAsync(new UpdateAdminUserInput(
            first.Id, first.UserName!, first.Email!, "Cliente", true, null, null, first.ConcurrencyStamp!));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var updated = await fixture.UserManager.FindByIdAsync(first.Id);
        Assert.NotNull(updated);
        Assert.False(await fixture.UserManager.IsInRoleAsync(updated, "Administrador"));
    }

    [Fact]
    public async Task May_reactivate_a_deactivated_user()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Operador", "operator@test.local", "Operador");
        var disabled = await fixture.Sut.SetActiveAsync(user.Id, false, user.ConcurrencyStamp!);
        var inactiveUser = await fixture.UserManager.FindByIdAsync(user.Id);

        var activated = await fixture.Sut.SetActiveAsync(user.Id, true, inactiveUser!.ConcurrencyStamp!);

        Assert.True(disabled.Succeeded);
        Assert.True(activated.Succeeded);
        Assert.True((await fixture.UserManager.FindByIdAsync(user.Id))!.IsActive);
    }

    [Fact]
    public async Task ResetPassword_uses_Identity_and_enforces_password_policy()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Operador", "operator@test.local", "Operador", "Old!Pass123");

        var weakResult = await fixture.Sut.ResetPasswordAsync(user.Id, "weak");
        var resetResult = await fixture.Sut.ResetPasswordAsync(user.Id, "New!Pass123");

        Assert.False(weakResult.Succeeded);
        Assert.True(resetResult.Succeeded, string.Join("; ", resetResult.Errors.Select(x => x.Description)));
        var updated = await fixture.UserManager.FindByIdAsync(user.Id);
        Assert.False(await fixture.UserManager.CheckPasswordAsync(updated!, "Old!Pass123"));
        Assert.True(await fixture.UserManager.CheckPasswordAsync(updated!, "New!Pass123"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            Db = db;
            UserManager = userManager;
            RoleManager = roleManager;
            Sut = new AdminUserService(db, userManager, roleManager);
        }

        public ApplicationDbContext Db { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public RoleManager<IdentityRole> RoleManager { get; }
        public AdminUserService Sut { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = await TestDbContextFactory.CreateAsync();
            var userManager = TestIdentityFactory.CreateUserManager(db);
            var roleManager = TestIdentityFactory.CreateRoleManager(db);
            foreach (var role in new[] { "Administrador", "Cliente", "Vendedor", "Operador" })
                await roleManager.CreateAsync(new IdentityRole(role));
            return new Fixture(db, userManager, roleManager);
        }

        public async Task<ApplicationUser> CreateUserAsync(string name, string email, string role, string password = "Valid!Pass123")
        {
            var user = new ApplicationUser { UserName = name, Email = email, IsActive = true };
            var result = await UserManager.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
            var roleResult = await UserManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded);
            return user;
        }

        public async Task<Customer> AddCustomerAsync(string name)
        {
            var customer = new Customer { LegalName = name, TradeName = name, Cnpj = Guid.NewGuid().ToString("N")[..14] };
            Db.Customers.Add(customer);
            await Db.SaveChangesAsync();
            return customer;
        }

        public async Task<SalesRepresentative> AddSellerAsync(string name)
        {
            var seller = new SalesRepresentative { Name = name, Email = $"{Guid.NewGuid():N}@test.local" };
            Db.SalesRepresentatives.Add(seller);
            await Db.SaveChangesAsync();
            return seller;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
