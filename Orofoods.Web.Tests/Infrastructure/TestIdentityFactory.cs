using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class TestIdentityFactory
{
    public static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var options = new IdentityOptions();
        options.User.AllowedUserNameCharacters = null!;
        options.Tokens.ProviderMap["Default"] = new TokenProviderDescriptor(typeof(TestPasswordResetTokenProvider));
        options.Tokens.PasswordResetTokenProvider = "Default";
        var services = new ServiceCollection()
            .AddSingleton<TestPasswordResetTokenProvider>()
            .BuildServiceProvider();
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(options),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services,
            new Logger<UserManager<ApplicationUser>>(new LoggerFactory()));
    }

    public static RoleManager<IdentityRole> CreateRoleManager(ApplicationDbContext db)
    {
        var store = new RoleStore<IdentityRole>(db);
        return new RoleManager<IdentityRole>(
            store,
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Logger<RoleManager<IdentityRole>>(new LoggerFactory()));
    }

    private sealed class TestPasswordResetTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult($"{user.Id}:{purpose}:test-token");

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(string.Equals(token, $"{user.Id}:{purpose}:test-token", StringComparison.Ordinal));

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
            Task.FromResult(true);
    }
}
