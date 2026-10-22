using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public sealed class AdminUserManagementServiceTests
{
    private const string ActorId = "admin-actor";

    [Fact]
    public async Task Create_stores_full_name_and_login_separately_and_records_safe_audit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.CreateAsync(new CreateAdminUserInput(
            "Cliente de homologação", "staging-customer-test@example.test", "Valid!Pass123", "Cliente", true,
            null, null, UserName: "staging-customer-test", MustChangePassword: true));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var user = await fixture.Users.FindByEmailAsync("staging-customer-test@example.test");
        Assert.NotNull(user);
        Assert.Equal("Cliente de homologação", user!.FullName);
        Assert.Equal("staging-customer-test", user.UserName);
        Assert.True(user.MustChangePassword);
        var audit = await fixture.Db.AdminUserChangeAudits.SingleAsync();
        Assert.Equal(ActorId, audit.ActorUserId);
        Assert.Equal(user.Id, audit.TargetUserId);
        Assert.Equal("Created", audit.Action);
        Assert.DoesNotContain("Valid!Pass123", audit.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_changes_full_name_and_login_independently_and_audits_fields()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Nome anterior", "login-anterior", "customer@test.local");

        var result = await fixture.Service.UpdateAsync(new UpdateAdminUserInput(
            user.Id, "Nome atualizado", "new@test.local", "Cliente", true, null, null,
            user.ConcurrencyStamp!, UserName: "login-atualizado"));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var updated = await fixture.Users.FindByIdAsync(user.Id);
        Assert.Equal("Nome atualizado", updated!.FullName);
        Assert.Equal("login-atualizado", updated.UserName);
        Assert.Equal("new@test.local", updated.Email);
        var audit = await fixture.Db.AdminUserChangeAudits.SingleAsync();
        Assert.Equal("Updated", audit.Action);
        Assert.Contains("nome", audit.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("login", audit.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("e-mail", audit.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_without_changes_does_not_rotate_security_stamp_or_write_audit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Nome", "login", "user@test.local");
        var securityStamp = user.SecurityStamp;

        var result = await fixture.Service.UpdateAsync(new UpdateAdminUserInput(
            user.Id, user.FullName, user.Email!, "Cliente", true, null, null, user.ConcurrencyStamp!, user.UserName));

        Assert.True(result.Succeeded);
        Assert.Equal(securityStamp, (await fixture.Users.FindByIdAsync(user.Id))!.SecurityStamp);
        Assert.Empty(await fixture.Db.AdminUserChangeAudits.ToListAsync());
    }

    [Fact]
    public async Task Temporary_password_reset_uses_identity_sets_change_flag_invalidates_stamp_and_hides_secrets()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Cliente", "customer-login", "customer@test.local");
        var originalStamp = user.SecurityStamp;

        var result = await fixture.Service.ResetPasswordAsync(user.Id, "Temporary!Pass123", true);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var updated = await fixture.Users.FindByIdAsync(user.Id);
        Assert.True(updated!.MustChangePassword);
        Assert.NotEqual(originalStamp, updated.SecurityStamp);
        Assert.True(await fixture.Users.CheckPasswordAsync(updated, "Temporary!Pass123"));
        var audit = await fixture.Db.AdminUserChangeAudits.SingleAsync();
        Assert.Equal("PasswordReset", audit.Action);
        Assert.DoesNotContain("Temporary!Pass123", audit.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", audit.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", audit.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_temporary_password_clears_gate_and_records_only_the_event()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Cliente", "customer-login", "customer@test.local");
        Assert.True((await fixture.Service.ResetPasswordAsync(user.Id, "Temporary!Pass123", true)).Succeeded);
        var temporaryUser = await fixture.Users.FindByIdAsync(user.Id);
        var originalStamp = temporaryUser!.SecurityStamp;

        var result = await fixture.Service.ChangeTemporaryPasswordAsync(user.Id, "Temporary!Pass123", "Permanent!Pass123");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        var updated = await fixture.Users.FindByIdAsync(user.Id);
        Assert.False(updated!.MustChangePassword);
        Assert.NotEqual(originalStamp, updated.SecurityStamp);
        Assert.True(await fixture.Users.CheckPasswordAsync(updated, "Permanent!Pass123"));
        Assert.Equal(new[] { "PasswordChanged", "PasswordReset" },
            (await fixture.Service.GetHistoryAsync(user.Id)).Select(entry => entry.Action));
    }

    [Fact]
    public async Task Changing_temporary_password_rejects_reusing_the_temporary_credential()
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = await fixture.CreateUserAsync("Cliente", "customer-login", "customer@test.local");
        Assert.True((await fixture.Service.ResetPasswordAsync(user.Id, "Temporary!Pass123", true)).Succeeded);

        var result = await fixture.Service.ChangeTemporaryPasswordAsync(
            user.Id, "Temporary!Pass123", "Temporary!Pass123");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "NewPasswordMustDiffer");
        var unchanged = await fixture.Users.FindByIdAsync(user.Id);
        Assert.True(unchanged!.MustChangePassword);
        Assert.True(await fixture.Users.CheckPasswordAsync(unchanged, "Temporary!Pass123"));
        Assert.Single(await fixture.Service.GetHistoryAsync(user.Id));
    }

    [Fact]
    public async Task History_returns_only_the_requested_users_latest_events()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CreateUserAsync("Primeiro", "first-login", "first@test.local");
        var second = await fixture.CreateUserAsync("Segundo", "second-login", "second@test.local");
        await fixture.Service.SetActiveAsync(first.Id, false, first.ConcurrencyStamp!);
        await fixture.Service.SetActiveAsync(second.Id, false, second.ConcurrencyStamp!);

        var history = await fixture.Service.GetHistoryAsync(first.Id);

        var entry = Assert.Single(history);
        Assert.Equal(first.Id, entry.TargetUserId);
        Assert.Equal("StatusChanged", entry.Action);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
        {
            Db = db;
            Users = users;
            Accessor = new TestHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, ActorId), new Claim(ClaimTypes.Name, "Administrador")], "tests"))
                }
            };
            Service = new AdminUserService(db, users, roles, Accessor);
        }

        public ApplicationDbContext Db { get; }
        public UserManager<ApplicationUser> Users { get; }
        public AdminUserService Service { get; }
        public IHttpContextAccessor Accessor { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = await TestDbContextFactory.CreateAsync();
            var users = TestIdentityFactory.CreateUserManager(db);
            var roles = TestIdentityFactory.CreateRoleManager(db);
            foreach (var role in new[] { "Administrador", "Cliente", "Vendedor", "Operador" })
                await roles.CreateAsync(new IdentityRole(role));
            return new Fixture(db, users, roles);
        }

        public async Task<ApplicationUser> CreateUserAsync(string name, string login, string email)
        {
            var user = new ApplicationUser { FullName = name, UserName = login, Email = email, IsActive = true };
            var result = await Users.CreateAsync(user, "Valid!Pass123");
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
            var roleResult = await Users.AddToRoleAsync(user, "Cliente");
            Assert.True(roleResult.Succeeded);
            return user;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
